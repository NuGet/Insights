using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchServiceUrlDiscoverer
    {
        private const int StartingPort = 44301;
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly IPortDiscoverer _portDiscoverer;

        public SearchServiceUrlDiscoverer(
            ServiceIndexCache serviceIndexCache,
            IPortDiscoverer portDiscoverer)
        {
            _serviceIndexCache = serviceIndexCache;
            _portDiscoverer = portDiscoverer;
        }

        public async Task<IReadOnlyList<string>> GetUrlsAsync(string serviceIndexType, bool specificInstances)
        {
            var urls = await _serviceIndexCache.GetUrlsAsync(serviceIndexType);

            if (specificInstances)
            {
                urls = await ExpandInstancePortsAsync(urls);
            }

            return urls
                .Distinct()
                .ToList();
        }

        private async Task<IReadOnlyList<string>> ExpandInstancePortsAsync(IReadOnlyList<string> urls)
        {
            // Parse all of the URLs as URIs.
            var uris = urls
                .Select(x => new Uri(x, UriKind.Absolute))
                .ToList();

            // Determine the available ports on each host.
            var hostTasks = uris
                .GroupBy(x => x.Host, StringComparer.OrdinalIgnoreCase)
                .Where(x => x
                    .Select(u => u.Scheme)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Single() != null)
                .Select(x => x.First())
                .Select(x => new
                {
                    x.Host,
                    PortsTask = _portDiscoverer.FindPortsAsync(
                        x.Host,
                        StartingPort,
                        requireSsl: x.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
                        connectTimeout: ConnectTimeout),
                })
                .ToList();
            await Task.WhenAll(hostTasks.Select(x => x.PortsTask));

            // Build a mapping from host to list of open instance ports.
            var hostToPorts = hostTasks.ToDictionary(
                x => x.Host,
                x => x.PortsTask.Result,
                StringComparer.OrdinalIgnoreCase);

            // Build URLs to the diagnostic endpoint on all search instances.
            var expandedUrls = uris
                .SelectMany(x => hostToPorts[x.Host]
                    .Select(p => new UriBuilder(x)
                    {
                        Port = p,
                    }))
                .Select(x => x.Uri.AbsoluteUri)
                .ToList();

            return expandedUrls;
        }
    }
}
