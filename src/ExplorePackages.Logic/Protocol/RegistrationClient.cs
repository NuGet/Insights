using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages
{
    public class RegistrationClient
    {
        private readonly HttpSource _httpSource;
        private readonly ILogger<RegistrationClient> _logger;

        public RegistrationClient(HttpSource httpSource, ILogger<RegistrationClient> logger)
        {
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<RegistrationLeaf> GetRegistrationLeafOrNullAsync(string baseUrl, string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            var leafUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/{normalizedVersion.ToLowerInvariant()}.json";
            return await _httpSource.DeserializeUrlAsync<RegistrationLeaf>(leafUrl, ignoreNotFounds: true, logger: _logger);
        }

        public async Task<RegistrationLeafItem> GetRegistrationLeafItemOrNullAsync(string baseUrl, string id, string version)
        {
            var result = await GetRegistrationLeafItemResultAsync(baseUrl, id, version, justExists: false);
            return result.Result;
        }

        public async Task<bool> HasPackageInIndexAsync(string baseUrl, string id, string version)
        {
            var result = await GetRegistrationLeafItemResultAsync(baseUrl, id, version, justExists: true);
            return result.Exists;
        }

        private async Task<RegistrationLeafItemResult> GetRegistrationLeafItemResultAsync(
            string baseUrl,
            string id,
            string version,
            bool justExists)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var registrationIndex = await GetRegistrationIndex(baseUrl, id);
            if (registrationIndex == null)
            {
                return new RegistrationLeafItemResult(exists: false, result: null);
            }

            foreach (var pageItem in registrationIndex.Items)
            {
                var lower = NuGetVersion.Parse(pageItem.Lower);
                var upper = NuGetVersion.Parse(pageItem.Upper);

                if ((lower == parsedVersion || upper == parsedVersion) && justExists)
                {
                    return new RegistrationLeafItemResult(exists: true, result: null);
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

                var leaf = leaves.FirstOrDefault(x => NuGetVersion.Parse(x.CatalogEntry.Version) == parsedVersion);
                if (leaf != null)
                {
                    return new RegistrationLeafItemResult(exists: true, result: leaf);
                }
            }

            return new RegistrationLeafItemResult(exists: false, result: null);
        }

        public async Task<RegistrationIndex> GetRegistrationIndex(string baseUrl, string id)
        {
            var indexUrl = $"{baseUrl.TrimEnd('/')}/{id.ToLowerInvariant()}/index.json";
            return await _httpSource.DeserializeUrlAsync<RegistrationIndex>(indexUrl, ignoreNotFounds: true, logger: _logger);
        }

        public async Task<RegistrationPage> GetRegistrationPage(string pageUrl)
        {
            return await _httpSource.DeserializeUrlAsync<RegistrationPage>(pageUrl, ignoreNotFounds: false, logger: _logger);
        }

        private class RegistrationLeafItemResult
        {
            public RegistrationLeafItemResult(bool exists, RegistrationLeafItem result)
            {
                Exists = exists;
                Result = result;
            }

            public bool Exists { get; }
            public RegistrationLeafItem Result { get; }
        }
    }
}
