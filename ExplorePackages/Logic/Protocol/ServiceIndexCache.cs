using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Logic
{
    public class ServiceIndexCache
    {
        private readonly Lazy<Task<ServiceIndexResourceV3>> _lazyServiceIndexResource;
        private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _urls
            = new ConcurrentDictionary<string, IReadOnlyList<string>>();

        public ServiceIndexCache()
        {
            _lazyServiceIndexResource = new Lazy<Task<ServiceIndexResourceV3>>(async () =>
            {
                var sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json", FeedType.HttpV3);
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
