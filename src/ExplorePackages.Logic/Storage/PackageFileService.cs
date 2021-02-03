using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.Pkcs;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using MessagePack;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.HighPerformance.Extensions;
using NuGet.Packaging.Signing;

namespace Knapcode.ExplorePackages
{
    public class PackageFileService
    {
        private readonly PackageWideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mzipFormat;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesSettings> _options;

        public PackageFileService(
            PackageWideEntityService wideEntityService,
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

        public async Task<PrimarySignature> GetPrimarySignatureAsync(CatalogLeafItem leafItem)
        {
            var info = await GetOrUpdateInfoAsync(leafItem);
            if (!info.Available)
            {
                return null;
            }

            using var srcStream = info.SignatureBytes.AsStream();
            return PrimarySignature.Load(srcStream);
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
            return await _wideEntityService.UpdateBatchAsync(
                _options.Value.PackageFileTableName,
                id,
                leafItems,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        public async Task<PackageFileInfoV1> GetOrUpdateInfoAsync(CatalogLeafItem leafItem)
        {
            return await _wideEntityService.GetOrUpdateInfoAsync(
                _options.Value.PackageFileTableName,
                leafItem,
                GetInfoAsync,
                OutputToData,
                DataToOutput);
        }

        private async Task<PackageFileInfoV1> GetInfoAsync(CatalogLeafItem leafItem)
        {
            if (leafItem.Type == CatalogLeafType.PackageDelete)
            {
                return MakeDeletedInfo(leafItem);
            }

            var url = await _flatContainerClient.GetPackageContentUrlAsync(leafItem.PackageId, leafItem.PackageVersion);

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

        private static PackageFileInfoV1 DataToOutput(PackageFileInfoVersions data)
        {
            return data.V1;
        }

        private static PackageFileInfoVersions OutputToData(PackageFileInfoV1 output)
        {
            return new PackageFileInfoVersions { V1 = output };
        }

        [MessagePackObject]
        public class PackageFileInfoVersions : PackageWideEntityService.IPackageWideEntity
        {
            [Key(0)]
            public PackageFileInfoV1 V1 { get; set; }

            DateTimeOffset PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
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
