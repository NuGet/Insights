using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.Insights
{
    public class ServiceIndexCache
    {
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly Lazy<Task<ServiceIndexResourceV3>> _lazyServiceIndexResource;
        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _urls
            = new ConcurrentDictionary<string, IReadOnlyList<string>>();

        public ServiceIndexCache(
            IOptions<NuGetInsightsSettings> options)
        {
            _options = options;
            _lazyServiceIndexResource = new Lazy<Task<ServiceIndexResourceV3>>(async () =>
            {
                var sourceRepository = Repository.Factory.GetCoreV3(_options.Value.V3ServiceIndex, FeedType.HttpV3);
                var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();
                return serviceIndexResource;
            });
        }

        public async Task<IReadOnlyList<string>> GetUrlsAsync(string type)
        {
            if (_urls.TryGetValue(type, out var urls))
            {
                return urls;
            }

            var serviceIndexResource = await _lazyServiceIndexResource.Value;
            urls = serviceIndexResource
                .GetServiceEntryUris(type)
                .Select(x => x.AbsoluteUri)
                .ToList();
            _urls.AddOrUpdate(type, urls, (key, value) => urls);

            return urls;
        }

        public async Task<string> GetUrlAsync(string type)
        {
            var urls = await GetUrlsAsync(type);
            return urls.FirstOrDefault();
        }
    }
}
