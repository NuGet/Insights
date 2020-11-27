using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class PackagesContainerClient
    {
        private readonly HttpSource _httpSource;
        private readonly ILogger<PackagesContainerClient> _logger;

        public PackagesContainerClient(HttpSource httpSource, ILogger<PackagesContainerClient> logger)
        {
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<BlobMetadata> GetPackageContentMetadataAsync(string baseUrl, string id, string version)
        {
            var packageUrl = GetPackageContentUrl(baseUrl, id, version);
            return await _httpSource.GetBlobMetadataAsync(packageUrl, _logger);
        }

        private static string GetPackageContentUrl(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var packageUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}.{normalizedVersion.ToLowerInvariant()}.nupkg";
            return packageUrl;
        }
    }
}
