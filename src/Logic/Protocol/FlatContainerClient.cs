using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class FlatContainerClient
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly HttpSource _httpSource;
        private readonly TempStreamService _tempStreamService;
        private readonly IOptions<ExplorePackagesSettings> _options;
        private readonly ILogger<FlatContainerClient> _logger;

        public FlatContainerClient(
            ServiceIndexCache serviceIndexCache,
            HttpSource httpSource,
            TempStreamService tempStreamService,
            IOptions<ExplorePackagesSettings> options,
            ILogger<FlatContainerClient> logger)
        {
            _serviceIndexCache = serviceIndexCache;
            _httpSource = httpSource;
            _tempStreamService = tempStreamService;
            _options = options;
            _logger = logger;
        }

        public async Task<BlobMetadata> GetPackageContentMetadataAsync(string baseUrl, string id, string version)
        {
            var url = GetPackageContentUrl(baseUrl, id, version);
            return await _httpSource.GetBlobMetadataAsync(url, _logger);
        }

        public async Task<string> GetPackageContentUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageContentUrl(baseUrl, id, version);
        }

        public string GetPackageContentUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg";
            return url;
        }

        public async Task<string> GetPackageManifestUrlAsync(string id, string version)
        {
            var baseUrl = await GetBaseUrlAsync();
            return GetPackageManifestUrl(baseUrl, id, version);
        }

        public string GetPackageManifestUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.nuspec";
            return url;
        }

        public async Task<TempStreamResult> DownloadPackageContentToFileAsync(string id, string version, CancellationToken token)
        {
            var url = await GetPackageContentUrlAsync(id, version);
            var nuGetLogger = _logger.ToNuGetLogger();
            var writer = _tempStreamService.GetWriter();

            TempStreamResult result = null;
            try
            {
                do
                {
                    result = await _httpSource.ProcessResponseAsync(
                        new HttpSourceRequest(url, nuGetLogger),
                        async response =>
                        {
                            if (response.StatusCode == HttpStatusCode.NotFound)
                            {
                                return null;
                            }

                            response.EnsureSuccessStatusCode();
                            using var networkStream = await response.Content.ReadAsStreamAsync();
                            return await writer.CopyToTempStreamAsync(
                                networkStream,
                                response.Content.Headers.ContentLength.Value,
                                IncrementalHash.CreateAll());
                        },
                        nuGetLogger,
                        token);

                    if (result == null)
                    {
                        return null;
                    }
                }
                while (result.Type == TempStreamResultType.NeedNewStream);

                return result;
            }
            catch
            {
                result?.Dispose();
                throw;
            }
        }

        public async Task<NuspecContext> GetNuspecContextAsync(string id, string version, CancellationToken token)
        {
            var baseUrl = await GetBaseUrlAsync();
            return await GetNuspecContextAsync(baseUrl, id, version, token);
        }

        public async Task<NuspecContext> GetNuspecContextAsync(string baseUrl, string id, string version, CancellationToken token)
        {
            var url = GetPackageManifestUrl(baseUrl, id, version);

            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, nuGetLogger)
                {
                    IgnoreNotFounds = true,
                },
                networkStream => Task.FromResult(NuspecContext.FromStream(id, version, networkStream, _logger)),
                nuGetLogger,
                token);
        }

        public string GetPackageIconUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var url = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/icon";
            return url;
        }

        public async Task<bool> HasPackageManifestAsync(string baseUrl, string id, string version)
        {
            var url = GetPackageManifestUrl(baseUrl, id, version);
            return await _httpSource.UrlExistsAsync(url, _logger);
        }

        public async Task<bool> HasPackageIconAsync(string baseUrl, string id, string version)
        {
            var url = GetPackageIconUrl(baseUrl, id, version);
            return await _httpSource.UrlExistsAsync(url, _logger);
        }

        public async Task<bool> HasPackageInIndexAsync(string baseUrl, string id, string version)
        {
            var index = await GetIndexAsync(baseUrl, id);
            if (index == null)
            {
                return false;
            }

            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            return index.Versions.Contains(lowerVersion);
        }

        public async Task<FlatContainerIndex> GetIndexAsync(string baseUrl, string id)
        {
            var lowerId = id.ToLowerInvariant();
            var packageUrl = $"{baseUrl.TrimEnd('/')}/{lowerId}/index.json";
            return await _httpSource.DeserializeUrlAsync<FlatContainerIndex>(packageUrl, ignoreNotFounds: true, logger: _logger);
        }

        private async Task<string> GetBaseUrlAsync()
        {
            if (_options.Value.FlatContainerBaseUrlOverride != null)
            {
                return _options.Value.FlatContainerBaseUrlOverride;
            }

            return await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.FlatContainer);
        }
    }
}
