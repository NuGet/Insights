using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class FlatContainerClient
    {
        private readonly HttpSource _httpSource;
        private readonly ILogger<FlatContainerClient> _logger;

        public FlatContainerClient(HttpSource httpSource, ILogger<FlatContainerClient> logger)
        {
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<BlobMetadata> GetPackageContentMetadataAsync(string baseUrl, string id, string version)
        {
            var packageUrl = GetPackageContentUrl(baseUrl, id, version);
            return await _httpSource.GetBlobMetadataAsync(packageUrl, _logger);
        }

        public string GetPackageContentUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var packageUrl = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.{lowerVersion}.nupkg";
            return packageUrl;
        }

        public string GetPackageManifestUrl(string baseUrl, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();
            var packageUrl = $"{baseUrl.TrimEnd('/')}/{lowerId}/{lowerVersion}/{lowerId}.nuspec";
            return packageUrl;
        }

        public async Task<bool> HasPackageManifestAsync(string baseUrl, string id, string version)
        {
            var packageUrl = GetPackageManifestUrl(baseUrl, id, version);
            return await _httpSource.UrlExistsAsync(packageUrl, _logger);
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
    }
}
