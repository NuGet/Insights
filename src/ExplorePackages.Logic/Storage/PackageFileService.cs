using System;
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

        public async Task<PackageFileInfo> GetOrUpdateInfoAsync(CatalogLeafItem leafItem)
        {
            var partitionKey = GetPartitionKey(leafItem.PackageId);
            var rowKey = GetRowKey(leafItem.PackageVersion);

            var existingEntity = await _wideEntityService.RetrieveAsync(_options.Value.PackageFileTableName, partitionKey, rowKey);
            if (existingEntity != null)
            {
                var existingInfo = Deserialize(existingEntity);
                if (existingInfo.CommitId == leafItem.CommitId
                    && existingInfo.CommitTimestamp == leafItem.CommitTimestamp)
                {
                    return existingInfo;
                }
            }

            var newInfo = await GetInfoAsync(leafItem);
            var newBytes = MessagePackSerializer.Serialize(newInfo, MessagePackSerializerOptions);

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
