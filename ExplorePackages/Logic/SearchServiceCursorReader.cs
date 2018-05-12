using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchServiceCursorReader
    {
        private readonly SearchServiceUrlDiscoverer _discoverer;
        private readonly SearchClient _client;
        private readonly ILogger<SearchServiceCursorReader> _logger;

        public SearchServiceCursorReader(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient client,
            ILogger<SearchServiceCursorReader> logger)
        {
            _discoverer = discoverer;
            _client = client;
            _logger = logger;
        }

        public async Task<DateTimeOffset> GetCursorAsync()
        {
            // Get all V2 search URLs, including ports to specific instances.
            var specificBaseUrls = await _discoverer.GetUrlsAsync(ServiceIndexTypes.V2Search, specificInstances: true);
            var loadBalancerBaseUrls = await _discoverer.GetUrlsAsync(ServiceIndexTypes.V2Search, specificInstances: false);
            var baseUrls = loadBalancerBaseUrls.Concat(specificBaseUrls);

            // Get all commit timestamps.
            var commitTimestampTasks = baseUrls
                .Select(GetTimestampAsync)
                .ToList();
            await Task.WhenAll(commitTimestampTasks);

            // Return the minimum.
            var commitTimestamp = commitTimestampTasks
                .Where(x => x.Result.HasValue)
                .Min(x => x.Result.Value);

            return commitTimestamp;
        }

        private async Task<DateTimeOffset?> GetTimestampAsync(string baseUrl)
        {
            try
            {
                var diagnostics = await _client.GetDiagnosticsAsync(baseUrl);

                return diagnostics.CommitUserData.CommitTimestamp;
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "A timeout occurred when getting the timestamp from {BaseUrl}.", baseUrl);
                return null;
            }
        }
    }
}
