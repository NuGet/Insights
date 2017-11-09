using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<bool> HasPackageAsync(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var packageUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}.{normalizedVersion.ToLowerInvariant()}.nupkg";

            return await _httpSource.ProcessResponseAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Head, packageUrl, _log))
                {
                    IgnoreNotFounds = true,
                },
                response =>
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return Task.FromResult(true);
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return Task.FromResult(false);
                    }

                    throw new HttpRequestException(
                        $"The request to {packageUrl} return HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                },
                _log,
                CancellationToken.None);
        }
    }
}
