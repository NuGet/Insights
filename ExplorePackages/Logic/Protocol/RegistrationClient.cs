using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class RegistrationClient
    {
        private static readonly JsonSerializer Serializer = new JsonSerializer
        {
            DateParseHandling = DateParseHandling.DateTimeOffset,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public RegistrationClient(HttpSource httpSource, ILogger log)
        {
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<bool> HasPackageLeafAsync(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var leafUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/{normalizedVersion.ToLowerInvariant()}.json";

            return await _httpSource.ProcessResponseAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Head, leafUrl, _log))
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
                        $"The request to {leafUrl} return HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
                },
                _log,
                CancellationToken.None);
        }

        public async Task<bool> HasPackageInIndexAsync(string baseUrl, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var registrationIndex = await GetRegistrationIndex(baseUrl, id);
            if (registrationIndex == null)
            {
                return false;
            }

            foreach (var pageItem in registrationIndex.Items)
            {
                var lower = NuGetVersion.Parse(pageItem.Lower);
                var upper = NuGetVersion.Parse(pageItem.Upper);

                if (lower == parsedVersion || upper == parsedVersion)
                {
                    return true;
                }

                if (parsedVersion < lower || parsedVersion > upper)
                {
                    continue;
                }

                List<RegistrationLeafItem> leaves = pageItem.Items;
                if (leaves == null)
                {
                    var page = await GetRegistrationPage(pageItem.Url);
                    leaves = page.Items;
                }

                if (leaves.Any(x => NuGetVersion.Parse(x.CatalogEntry.Version) == parsedVersion))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task<RegistrationIndex> GetRegistrationIndex(string baseUrl, string id)
        {
            var indexUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/index.json";
            return await DeserializeUrlAsync<RegistrationIndex>(indexUrl, ignoreNotFounds: true);
        }

        public async Task<RegistrationPage> GetRegistrationPage(string pageUrl)
        {
            return await DeserializeUrlAsync<RegistrationPage>(pageUrl, ignoreNotFounds: false);
        }

        private async Task<T> DeserializeUrlAsync<T>(string url, bool ignoreNotFounds)
        {
            return await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, _log)
                {
                    IgnoreNotFounds = ignoreNotFounds,
                },
                stream =>
                {
                    if (stream == null)
                    {
                        return Task.FromResult(default(T));
                    }

                    using (var textReader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(textReader))
                    {
                        var result = Serializer.Deserialize<T>(jsonReader);
                        return Task.FromResult(result);
                    }
                },
                _log,
                CancellationToken.None);
        }
    }
}
