using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights
{
    public class SearchServiceCursorReader
    {
        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly SearchClient _client;
        private readonly ILogger<SearchServiceCursorReader> _logger;

        public SearchServiceCursorReader(
            ServiceIndexCache serviceIndexCache,
            SearchClient client,
            ILogger<SearchServiceCursorReader> logger)
        {
            _serviceIndexCache = serviceIndexCache;
            _client = client;
            _logger = logger;
        }

        public async Task<DateTimeOffset> GetCursorAsync()
        {
            // Get all V2 search URLs, including ports to specific instances.
            var baseUrls = await _serviceIndexCache.GetUrlsAsync(ServiceIndexTypes.V2Search);

            // Get all commit timestamps.
            var commitTimestampTasks = baseUrls
                .Select(GetTimestampAsync)
                .ToList();
            await Task.WhenAll(commitTimestampTasks);

            // Return the minimum.
            var commitTimestamp = commitTimestampTasks
                .Min(x => x.Result);

            return commitTimestamp;
        }

        private async Task<DateTimeOffset> GetTimestampAsync(string baseUrl)
        {
            var diagnostics = await _client.GetDiagnosticsAsync(baseUrl);
            return new[]
            {
                diagnostics.SearchIndex.LastCommitTimestamp,
                diagnostics.HijackIndex.LastCommitTimestamp,
            }.Min();
        }
    }
}
