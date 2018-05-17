using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecStore
    {
        private const int BufferSize = 8192;
        
        private readonly IFileStorageService _fileStorageService;
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly FlatContainerClient _flatContainerClient;
        private readonly HttpSource _httpSource;
        private readonly ILogger<NuspecStore> _logger;

        public NuspecStore(
            IFileStorageService fileStorageService,
            ServiceIndexCache serviceIndexCache,
            FlatContainerClient flatContainerClient,
            HttpSource httpSource,
            ILogger<NuspecStore> logger)
        {
            _fileStorageService = fileStorageService;
            _serviceIndexCache = serviceIndexCache;
            _flatContainerClient = flatContainerClient;
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<bool> StoreNuspecAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
            var url = _flatContainerClient.GetPackageManifestUrl(baseUrl, id, version);

            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, nuGetLogger)
                {
                    IgnoreNotFounds = true,
                },
                async networkStream =>
                {
                    if (networkStream == null)
                    {
                        return false;
                    }

                    await _fileStorageService.StoreNuspecStreamAsync(
                        id,
                        version,
                        destStream => networkStream.CopyToAsync(destStream));

                    return true;
                },
                nuGetLogger,
                token);
        }

        public async Task<NuspecContext> GetNuspecContextAsync(string id, string version)
        {
            using (var stream = await _fileStorageService.GetNuspecStreamOrNullAsync(id, version))
            {
                if (stream == null)
                {
                    return new NuspecContext(exists: false, document: null);
                }

                try
                {
                    var document = XmlUtility.LoadXml(stream);
                    return new NuspecContext(exists: true, document: document);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not parse .nuspec for {Id} {Version}.", id, version);
                    throw;
                }
            }
        }
    }
}
