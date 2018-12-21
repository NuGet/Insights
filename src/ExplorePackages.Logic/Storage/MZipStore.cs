using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.MiniZip;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

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
                    destStream => _mZipFormat.WriteAsync(reader.Stream, destStream),
                    accessCondition: AccessCondition.GenerateEmptyCondition());
            }
        }

        public async Task<MZipContext> GetMZipContextAsync(string id, string version)
        {
            using (var stream = await _fileStorageService.GetStreamOrNullAsync(id, version, FileArtifactType.MZip))
            {
                if (stream == null)
                {
                    return new MZipContext(exists: false, size: null, zipDirectory: null);
                }

                try
                {
                    using (var zipStream = await _mZipFormat.ReadAsync(stream))
                    using (var reader = new ZipDirectoryReader(zipStream))
                    {
                        var zipDirectory = await reader.ReadAsync();
                        return new MZipContext(exists: true, size: stream.Length, zipDirectory: zipDirectory);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not parse .mzip for {Id} {Version}.", id, version);
                    throw;
                }
            }
        }
    }
}
