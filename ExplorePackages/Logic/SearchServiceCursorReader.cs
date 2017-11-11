using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchServiceCursorReader
    {
        private const string Type = "SearchQueryService";
        private const int StartingPort = 44301;
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly PortDiscoverer _portDiscoverer;
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public SearchServiceCursorReader(
            ServiceIndexCache serviceIndexCache,
            PortDiscoverer portDiscoverer,
            HttpSource httpSource,
            ILogger log)
        {
            _serviceIndexCache = serviceIndexCache;
            _portDiscoverer = portDiscoverer;
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<DateTimeOffset> GetCursorAsync()
        {
            var urls = await _serviceIndexCache.GetUrlsAsync(Type);

            // Discovery the maximum port for all search services.
            var hostTasks = urls
                .Select(x => new Uri(x, UriKind.Absolute))
                .GroupBy(x => x.Host, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Select(x => new
                {
                    Uri = x,
                    MaximumPortTask = _portDiscoverer.FindMaximumPortAsync(
                        x.Host,
                        StartingPort,
                        requireSsl: x.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
                        connectTimeout: ConnectTimeout),
                })
                .ToList();
            await Task.WhenAll(hostTasks.Select(x => x.MaximumPortTask));

            // Build URLs to the diagnostic endpoint on all search instances.
            var searchDiagUrls = hostTasks
                .Where(x => x.MaximumPortTask.Result.HasValue)
                .SelectMany(x => Enumerable
                    .Range(StartingPort, x.MaximumPortTask.Result.Value - StartingPort + 1)
                    .Select(port => new UriBuilder(x.Uri)
                    {
                        Port = port,
                        Path = "/search/diag"
                    }))
                .Select(x => x.Uri.AbsoluteUri)
                .ToList();

            // Get all commit timestamps.
            var commitTimestampTasks = searchDiagUrls
                .Select(x => GetCursorAsync(x))
                .ToList();
            await Task.WhenAll(commitTimestampTasks);

            var commitTimestamp = commitTimestampTasks.Min(x => x.Result);
            return commitTimestamp;
        }

        private async Task<DateTimeOffset> GetCursorAsync(string searchDiagUrl)
        {
            var searchDiag = await _httpSource.DeserializeUrlAsync<SearchDiag>(
                searchDiagUrl,
                ignoreNotFounds: false,
                log: _log);

            return searchDiag.CommitUserData.CommitTimestamp;
        }

        private class SearchDiag
        {
            [JsonProperty("CommitUserData")]
            public CommitUserData CommitUserData { get; set; }
        }

        private class CommitUserData
        {
            [JsonProperty("commitTimeStamp")]
            public DateTimeOffset CommitTimestamp { get; set; }
        }
    }
}
