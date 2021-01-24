using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
        private static readonly MessagePackSerializerOptions MessagePackSerializerOptions = MessagePackSerializerOptions
            .Standard
            .WithCompression(MessagePackCompression.Lz4Block);

        private readonly WideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mzipFormat;
        private readonly IOptions<ExplorePackagesSettings> _options;

        public PackageFileService(
            WideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            MZipFormat mzipFormat,
            IOptions<ExplorePackagesSettings> options)
        {
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _mzipFormat = mzipFormat;
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

        public async Task<IReadOnlyDictionary<CatalogLeafItem, PackageFileInfo>> UpdateBatchAsync(string id, IReadOnlyCollection<CatalogLeafItem> leafItems)
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
            var output = new Dictionary<CatalogLeafItem, PackageFileInfo>();
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

            await _wideEntityService.ExecuteBatchAsync(_options.Value.PackageFileTableName, batch);

            return output;
        }

        public async Task<PackageFileInfo> GetOrUpdateInfoAsync(CatalogLeafItem leafItem)
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

        private static byte[] Serialize(PackageFileInfo newInfo)
        {
            return MessagePackSerializer.Serialize(newInfo, MessagePackSerializerOptions);
        }

        private async Task<(WideEntity ExistingEntity, PackageFileInfo MatchingInfo)> GetExistingAsync(string partitionKey, string rowKey, CatalogLeafItem leafItem)
        {
            var existingEntity = await _wideEntityService.RetrieveAsync(_options.Value.PackageFileTableName, partitionKey, rowKey);
            if (existingEntity != null)
            {
                var existingInfo = Deserialize(existingEntity);
                if (existingInfo.CommitId == leafItem.CommitId
                    && existingInfo.CommitTimestamp == leafItem.CommitTimestamp)
                {
                    return (existingEntity, existingInfo);
                }

                return (existingEntity, null);
            }

            return (null, null);
        }

        private async Task<PackageFileInfo> GetInfoAsync(CatalogLeafItem leafItem)
        {
            var url = await GetPackageUrlAsync(leafItem.PackageId, leafItem.PackageVersion);
            using var destStream = new MemoryStream();
            PackageFileInfo newInfo;
            try
            {
                using var reader = await _httpZipProvider.GetReaderAsync(new Uri(url));
                await _mzipFormat.WriteAsync(reader.Stream, destStream);
                newInfo = new PackageFileInfo
                {
                    CommitId = leafItem.CommitId,
                    CommitTimestamp = leafItem.CommitTimestamp,
                    Available = true,
                    HttpHeaders = reader.Properties,
                    MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                };
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                newInfo = new PackageFileInfo
                {
                    CommitId = leafItem.CommitId,
                    CommitTimestamp = leafItem.CommitTimestamp,
                    Available = false,
                };
            }

            return newInfo;
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

        private static PackageFileInfo Deserialize(WideEntity entity)
        {
            return MessagePackSerializer.Deserialize<PackageFileInfo>(entity.GetStream(), MessagePackSerializerOptions);
        }

        [MessagePackObject]
        public class PackageFileInfo
        {
            [Key(0)]
            public string CommitId { get; set; }

            [Key(1)]
            public DateTimeOffset CommitTimestamp { get; set; }

            [Key(2)]
            public bool Available { get; set; }

            [Key(3)]
            public ILookup<string, string> HttpHeaders { get; set; }

            [Key(4)]
            public Memory<byte> MZipBytes { get; set; }
        }
    }
}
