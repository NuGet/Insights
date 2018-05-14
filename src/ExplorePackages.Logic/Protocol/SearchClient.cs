using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchClient
    {
        private readonly HttpSource _httpSource;
        private readonly ISearchServiceUrlCacheInvalidator _invalidator;
        private readonly ILogger<SearchClient> _logger;

        public SearchClient(
            HttpSource httpSource,
            ISearchServiceUrlCacheInvalidator invalidator,
            ILogger<SearchClient> logger)
        {
            _httpSource = httpSource;
            _invalidator = invalidator;
            _logger = logger;
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
                    logger: _logger);
            }
            catch
            {
                _invalidator.InvalidateCache();
                throw;
            }
        }

        public async Task<V2SearchResultItem> GetPackageOrNullAsync(string baseUrl, string id, string version, bool semVer2, int maxTries)
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
                    maxTries: maxTries,
                    logger: _logger);
            }
            catch
            {
                _invalidator.InvalidateCache();
                throw;
            }

            if (result.TotalHits == 0)
            {
                return null;
            }
            else if (result.TotalHits == 1)
            {
                return result.Data[0];
            }

            // Account for ToLower normalization (used instead of ToLowerInvariant in this case).
            var idCount = result
                .Data
                .Select(x => x.PackageRegistration.Id.ToLower())
                .Distinct()
                .Count();
            var exactMatch = result
                .Data
                .FirstOrDefault(x => x.PackageRegistration.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (idCount == 1 && exactMatch != null)
            {
                return exactMatch;
            }

            throw new InvalidDataException("The count returned by V2 search should be either 0 or 1.");
        }
    }
}
