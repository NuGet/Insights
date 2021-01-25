using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.Pkcs;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.WideEntities;
using Knapcode.MiniZip;
using MessagePack;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.HighPerformance.Extensions;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class PackageFileService
    {
        public static readonly MessagePackSerializerOptions MessagePackSerializerOptions = MessagePackSerializerOptions
            .Standard
            .WithCompression(MessagePackCompression.Lz4Block);

        private readonly WideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mzipFormat;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesSettings> _options;

        public PackageFileService(
            WideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            MZipFormat mzipFormat,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesSettings> options)
        {
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _mzipFormat = mzipFormat;
            _telemetryClient = telemetryClient;
            _options = options;
        }

        public async Task InitializeAsync()
        {
            await _wideEntityService.InitializeAsync(_options.Value.PackageFileTableName);
        }

        public async Task<ZipDirectory> GetZipDirectoryAsync(CatalogLeafItem leafItem)
        {
            var info = await GetOrUpdateInfoAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            using var srcStream = info.MZipBytes.AsStream();
            using var destStream = await _mzipFormat.ReadAsync(srcStream);
            var reader = new ZipDirectoryReader(destStream);
            return await reader.ReadAsync();
        }

        public async Task<IReadOnlyDictionary<CatalogLeafItem, PackageFileInfoV1>> UpdateBatchAsync(string id, IReadOnlyCollection<CatalogLeafItem> leafItems)
        {
            var rowKeyToLeafItem = new Dictionary<string, CatalogLeafItem>();
            foreach (var leafItem in leafItems)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(id, leafItem.PackageId))
                {
                    throw new ArgumentException("All leaf items must have the same package ID.");
                }

                var rowKey = GetRowKey(leafItem.PackageVersion);
                if (rowKeyToLeafItem.ContainsKey(rowKey))
                {
                    throw new ArgumentException("The leaf items must be unique by package version.");
                }

                rowKeyToLeafItem.Add(rowKey, leafItem);
            }

            var partitionKey = GetPartitionKey(id);

            // Fetch get the latest data for all leaf items, where applicable. There are three possibilities for each
            // row keys:
            //   1. The row key does not exist. This means we must fetch the info and insert it into the table.
            //   2. The row exists but the data is stale. This means we must fetch the info and replace it in the table.
            //   3. The row exists and is not stale. We can just return the data in the table.
            var batch = new List<WideEntityOperation>();
            var output = new Dictionary<CatalogLeafItem, PackageFileInfoV1>();
            foreach (var (rowKey, leafItem) in rowKeyToLeafItem)
            {
                (var existingEntity, var matchingInfo) = await GetExistingAsync(partitionKey, rowKey, leafItem);
                if (matchingInfo == null)
                {
                    var newInfo = await GetInfoAsync(leafItem);
                    var newBytes = Serialize(newInfo);
                    if (existingEntity == null)
                    {
                        batch.Add(WideEntityOperation.Insert(partitionKey, rowKey, newBytes));
                    }
                    else
                    {
                        batch.Add(WideEntityOperation.Replace(existingEntity, newBytes));
                    }

                    output.Add(leafItem, newInfo);
                }
                else
                {
                    output.Add(leafItem, matchingInfo);
                }
            }

            await _wideEntityService.ExecuteBatchAsync(_options.Value.PackageFileTableName, batch, allowBatchSplits: true);

            return output;
        }

        public async Task<PackageFileInfoV1> GetOrUpdateInfoAsync(CatalogLeafItem leafItem)
        {
            var partitionKey = GetPartitionKey(leafItem.PackageId);
            var rowKey = GetRowKey(leafItem.PackageVersion);

            (var existingEntity, var matchingInfo) = await GetExistingAsync(partitionKey, rowKey, leafItem);
            if (matchingInfo != null)
            {
                return matchingInfo;
            }

            var newInfo = await GetInfoAsync(leafItem);
            var newBytes = Serialize(newInfo);

            if (existingEntity != null)
            {
                await _wideEntityService.ReplaceAsync(
                    _options.Value.PackageFileTableName,
                    existingEntity,
                    newBytes);
            }
            else
            {
                await _wideEntityService.InsertAsync(
                    _options.Value.PackageFileTableName,
                    partitionKey,
                    rowKey,
                    newBytes);
            }

            return newInfo;
        }

        private static byte[] Serialize(PackageFileInfoV1 newInfo)
        {
            return MessagePackSerializer.Serialize(
                new PackageFileInfoVersions { V1 = newInfo },
                MessagePackSerializerOptions);
        }

        private async Task<(WideEntity ExistingEntity, PackageFileInfoV1 MatchingInfo)> GetExistingAsync(string partitionKey, string rowKey, CatalogLeafItem leafItem)
        {
            var existingEntity = await _wideEntityService.RetrieveAsync(_options.Value.PackageFileTableName, partitionKey, rowKey);
            if (existingEntity != null)
            {
                var existingInfo = Deserialize(existingEntity);

                // Prefer the existing entity if not older than the current leaf item
                if (leafItem.CommitTimestamp <= existingInfo.CommitTimestamp)
                {
                    return (existingEntity, existingInfo);
                }

                return (existingEntity, null);
            }

            return (null, null);
        }

        private async Task<PackageFileInfoV1> GetInfoAsync(CatalogLeafItem leafItem)
        {
            if (leafItem.Type == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            var url = await GetPackageUrlAsync(leafItem.PackageId, leafItem.PackageVersion);

            var metric = _telemetryClient.GetMetric($"{nameof(PackageFileService)}.{nameof(GetInfoAsync)}.DurationMs");
            var sw = Stopwatch.StartNew();

            using var destStream = new MemoryStream();
            try
            {
                using var reader = await _httpZipProvider.GetReaderAsync(new Uri(url));

                var zipDirectory = await reader.ReadAsync();
                var signatureEntry = zipDirectory.Entries.Single(x => x.GetName() == ".signature.p7s");
                var signatureBytes = await reader.ReadUncompressedFileDataAsync(zipDirectory, signatureEntry);

                var signedCms = new SignedCms();
                signedCms.Decode(signatureBytes);

                await _mzipFormat.WriteAsync(reader.Stream, destStream);
                return new PackageFileInfoV1
                {
                    CommitTimestamp = leafItem.CommitTimestamp,
                    Available = true,
                    HttpHeaders = reader.Properties,
                    MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                    SignatureBytes = signatureBytes.AsMemory(),
                };
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return MakeDeletedInfo(leafItem);
            }
            finally
            {
                metric.TrackValue(sw.ElapsedMilliseconds);
            }
        }

        private static PackageFileInfoV1 MakeDeletedInfo(CatalogLeafItem leafItem)
        {
            return new PackageFileInfoV1
            {
                CommitTimestamp = leafItem.CommitTimestamp,
                Available = false,
            };
        }

        private static string GetPartitionKey(string id)
        {
            return id.ToLowerInvariant();
        }

        private static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }
        private async Task<string> GetPackageUrlAsync(string id, string version)
        {
            return await _flatContainerClient.GetPackageContentUrlAsync(id, version);
        }

        private static PackageFileInfoV1 Deserialize(WideEntity entity)
        {
            var info = MessagePackSerializer.Deserialize<PackageFileInfoVersions>(entity.GetStream(), MessagePackSerializerOptions);
            return info.V1;
        }

        [MessagePackObject]
        public class PackageFileInfoVersions
        {
            [Key(0)]
            public PackageFileInfoV1 V1 { get; set; }
        }

        [MessagePackObject]
        public class PackageFileInfoV1
        {
            [Key(1)]
            public DateTimeOffset CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string> HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> MZipBytes { get; set; }

            [Key(5)]
            public Memory<byte> SignatureBytes { get; set; }
        }
    }
}
