using System;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchServiceCursorReader
    {
        private readonly SearchServiceUrlDiscoverer _discoverer;
        private readonly SearchClient _client;

        public SearchServiceCursorReader(
            SearchServiceUrlDiscoverer discoverer,
            SearchClient client)
        {
            _discoverer = discoverer;
            _client = client;
        }

        public async Task<DateTimeOffset> GetCursorAsync()
        {
            // Get all V2 search URLs, including ports to specific instances.
            var baseUrls = await _discoverer.GetUrlsAsync(ServiceIndexTypes.V2Search, specificInstances: true);

            // Get all commit timestamps.
            var commitTimestampTasks = baseUrls
                .Select(x => _client.GetDiagnosticsAsync(x))
                .ToList();
            await Task.WhenAll(commitTimestampTasks);

            // Return the minimum.
            var commitTimestamp = commitTimestampTasks
                .Select(x => x.Result.CommitUserData.CommitTimestamp)
                .Min();

            return commitTimestamp;
        }
    }
}
