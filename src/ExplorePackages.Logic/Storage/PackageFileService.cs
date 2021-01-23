using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.WideEntities;
using Knapcode.MiniZip;
using MessagePack;
using Microsoft.Extensions.Options;
using NuGet.Versioning;
using Microsoft.Toolkit.HighPerformance.Extensions;

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

        public async Task<ZipDirectory> GetZipDirectoryCachedAsync(string id, string version)
        {
            var entity = await _wideEntityService.RetrieveAsync(
                _options.Value.PackageFileTableName,
                GetPartitionKey(id),
                GetRowKey(version),
                includeData: true);

            var info = await MessagePackSerializer.DeserializeAsync<PackageFileInfo>(
                entity.GetStream(),
                MessagePackSerializerOptions);

            if (!info.Available)
            {
                return null;
            }

            using var zipStream = await _mzipFormat.ReadAsync(info.MZipBytes.AsStream());
            using var zipDirectoryReader = new ZipDirectoryReader(zipStream);
            return await zipDirectoryReader.ReadAsync();
        }

        public async Task UpdateAsync(string id, string version)
        {
            var url = await GetPackageUrlAsync(id, version);
            using var destStream = new MemoryStream();
            PackageFileInfo info;
            try
            {
                using var reader = await _httpZipProvider.GetReaderAsync(new Uri(url));
                await _mzipFormat.WriteAsync(reader.Stream, destStream);
                info = new PackageFileInfo
                {
                    Available = true,
                    HttpHeaders = reader.Properties,
                    MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, (int)destStream.Length),
                };
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                info = new PackageFileInfo
                {
                    Available = false,
                };
            }

            var serializedBytes = MessagePackSerializer.Serialize(info, MessagePackSerializerOptions);

            await _wideEntityService.InsertOrReplaceAsync(
                _options.Value.PackageFileTableName,
                GetPartitionKey(id),
                GetRowKey(version),
                serializedBytes);
        }

        private static string GetPartitionKey(string id)
        {
            return id.ToLowerInvariant();
        }

        private static string GetRowKey(string version)
        {
            return NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
        }

        public async Task<ZipDirectory> GetZipDirectoryAsync(string id, string version)
        {
            var url = await GetPackageUrlAsync(id, version);
            try
            {
                using var reader = await _httpZipProvider.GetReaderAsync(new Uri(url));
                return await reader.ReadAsync();
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task<string> GetPackageUrlAsync(string id, string version)
        {
            return await _flatContainerClient.GetPackageContentUrlAsync(id, version);
        }

        [MessagePackObject]
        private class PackageFileInfo
        {
            [Key(0)]
            public bool Available { get; set; }

            [Key(1)]
            public ILookup<string, string> HttpHeaders { get; set; }

            [Key(2)]
            public Memory<byte> MZipBytes { get; set; }
        }
    }
}
