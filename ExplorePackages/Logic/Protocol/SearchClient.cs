using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using NuGet.Common;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchClient
    {
        private readonly HttpSource _httpSource;
        private readonly ISearchServiceUrlCacheInvalidator _invalidator;
        private readonly ILogger _log;

        public SearchClient(
            HttpSource httpSource,
            ISearchServiceUrlCacheInvalidator invalidator,
            ILogger log)
        {
            _httpSource = httpSource;
            _invalidator = invalidator;
            _log = log;
        }

        public async Task<SearchDiagnostics> GetDiagnosticsAsync(string baseUrl)
        {
            var url = $"{baseUrl.TrimEnd('/')}/search/diag";

            try
            {
                return await _httpSource.DeserializeUrlAsync<SearchDiagnostics>(
                    url,
                    ignoreNotFounds: false,
                    maxTries: 1,
                    log: _log);
            }
            catch
            {
                _invalidator.InvalidateCache();
                throw;
            }
        }

        public async Task<bool> HasPackageAsync(string baseUrl, string id, string version, bool semVer2)
        {
            var semVerLevel = semVer2 ? "2.0.0" : "1.0.0";
            var query = $"packageid:{id} version:{version}";
            var url = $"{baseUrl.TrimEnd('/')}/search/query?q={Uri.EscapeDataString(query)}&take=100&ignoreFilter=true&semVerLevel={semVerLevel}";

            V2SearchResult result;
            try
            {
                result = await _httpSource.DeserializeUrlAsync<V2SearchResult>(
                    url,
                    ignoreNotFounds: false,
                    maxTries: 1,
                    log: _log);
            }
            catch
            {
                _invalidator.InvalidateCache();
                throw;
            }

            if (result.TotalHits == 0)
            {
                return false;
            }
            else if (result.TotalHits == 1)
            {
                return true;
            }

            // Account for ToLower normalization (used instead of ToLowerInvariant in this case).
            var idCount = result
                .Data
                .Select(x => x.PackageRegistration.Id.ToLower())
                .Distinct()
                .Count();
            var hasExactId = result
                .Data
                .Any(x => x.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (idCount == 1 && hasExactId)
            {
                return true;
            }

            throw new InvalidDataException("The count returned by V2 search should be either 0 or 1.");
        }
    }
}
