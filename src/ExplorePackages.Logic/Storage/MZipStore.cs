using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class MZipStore
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpZipProvider _httpZipProvider;
        private readonly MZipFormat _mZipFormat;
        private readonly ILogger<MZipStore> _logger;

        public MZipStore(
            IFileStorageService fileStorageService,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpZipProvider httpZipProvider,
            MZipFormat mZipFormat,
            ILogger<MZipStore> logger)
        {
            _fileStorageService = fileStorageService;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpZipProvider = httpZipProvider;
            _mZipFormat = mZipFormat;
            _logger = logger;
        }

        public async Task StoreMZipAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var url = _flatContainerClient.GetPackageContentUrl(baseUrl, id, version);

            using (var reader = await _httpZipProvider.GetReaderAsync(new Uri(url)))
            {
                await _fileStorageService.StoreStreamAsync(
                    id,
                    version,
                    FileArtifactType.MZip,
                    destStream => _mZipFormat.WriteAsync(reader.Stream, destStream));
            }
        }

        public async Task<Stream> GetMZipStreamOrNullAsync(string id, string version, CancellationToken token)
        {
            var stream = await _fileStorageService.GetStreamOrNullAsync(
                id,
                version,
                FileArtifactType.MZip);

            if (stream == null)
            {
                return null;
            }

            return await _mZipFormat.ReadAsync(stream);
        }
    }
}
