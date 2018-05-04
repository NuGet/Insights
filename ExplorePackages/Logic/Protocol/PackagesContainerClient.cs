using System.Net.Http;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackagesContainerClient
    {
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public PackagesContainerClient(HttpSource httpSource, ILogger log)
        {
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<BlobMetadata> GetPackageContentMetadataAsync(string baseUrl, string id, string version)
        {
            var packageUrl = GetPackageContentUrl(baseUrl, id, version);
            return await _httpSource.GetBlobMetadataAsync(packageUrl, _log);
        }

        private static string GetPackageContentUrl(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var packageUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}.{normalizedVersion.ToLowerInvariant()}.nupkg";
            return packageUrl;
        }
    }
}
