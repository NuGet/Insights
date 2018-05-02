using NuGet.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchServiceCursorReader
    {
        private readonly SearchServiceUrlDiscoverer _discoverer;
        private readonly SearchClient _client;
        private readonly ILogger _log;

        public SearchServiceCursorReader(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient client,
            ILogger log)
        {
            _discoverer = discoverer;
            _client = client;
            _log = log;
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
            catch (TimeoutException)
            {
                _log.LogWarning($"A timeout occurred when getting the timestamp from {baseUrl}.");
                return null;
            }
        }
    }
}
