using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecStore
    {        
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

                    await _fileStorageService.StoreStreamAsync(
                        id,
                        version,
                        FileArtifactType.Nuspec,
                        destStream => networkStream.CopyToAsync(destStream),
                        accessCondition: AccessCondition.GenerateEmptyCondition());

                    return true;
                },
                nuGetLogger,
                token);
        }

        public async Task<NuspecContext> GetNuspecContextAsync(string id, string version)
        {
            using (var stream = await _fileStorageService.GetStreamOrNullAsync(id, version, FileArtifactType.Nuspec))
            {
                return NuspecContext.FromStream(id, version, stream, _logger);
            }
        }
    }
}
