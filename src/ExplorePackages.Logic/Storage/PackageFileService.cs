using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Knapcode.ExplorePackages.WideEntities;
using Knapcode.MiniZip;
using MessagePack;

namespace Knapcode.ExplorePackages
{
    public class PackageFileService
    {
        [MessagePackObject]
        private class PackageFileInfo
        {
            [Key(0)]
            public bool Deleted { get; set; }

            [Key(1)]
            public ILookup<string, string> HttpHeaders { get; set; }

            [Key(2)]
            public Memory<byte> MZipBytes { get; set; }
        }

        private readonly WideEntityService _wideEntityService;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mzipFormat;

        public PackageFileService(
            WideEntityService wideEntityService,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            MZipFormat mzipFormat)
        {
            _wideEntityService = wideEntityService;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _mzipFormat = mzipFormat;
        }

        /*
        public async Task<ZipDirectory> GetZipDirectoryAsync(CatalogLeafItem leafItem)
        {
            var url = await GetPackageUrlAsync(leafItem.PackageId, leafItem.PackageVersion);
            using var destStream = new MemoryStream();
            var info = new PackageFileInfo();
            try
            {
                using var reader = await _httpZipProvider.GetReaderAsync(new Uri(url));
                await _mzipFormat.WriteAsync(reader.Stream, destStream);
                info.HttpHeaders = reader.Properties;
                info.Deleted = false;
                info.MZipBytes = new Memory<byte>(destStream.GetBuffer(), 0, destStream.Length);
            }
            catch (MiniZipHttpStatusCodeException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

        }
        */

        public async Task<ZipDirectory> GetZipDirectoryAsync(string id, string version)
        {
            var url = await _flatContainerClient.GetPackageContentUrlAsync(id, version);
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
    }
}
