using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Logic
{
    public class ServiceIndexCache
    {
        private readonly Lazy<Task<ServiceIndexResourceV3>> _lazyServiceIndexResource;
        private readonly ConcurrentDictionary<string, string> _urls = new ConcurrentDictionary<string, string>();

        public ServiceIndexCache()
        {
            _lazyServiceIndexResource = new Lazy<Task<ServiceIndexResourceV3>>(async () =>
            {
                var sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json", FeedType.HttpV3);
                var serviceIndexResource = await sourceRepository.GetResourceAsync<ServiceIndexResourceV3>();
                return serviceIndexResource;
            });
        }

        public async Task<string> GetUrlAsync(string type)
        {
            if (_urls.TryGetValue(type, out string url))
            {
                return url;
            }

            var serviceIndexResource = await _lazyServiceIndexResource.Value;
            url = serviceIndexResource.GetServiceEntryUri(type)?.AbsoluteUri;
            _urls.AddOrUpdate(type, url, (key, value) => url);

            return url;
        }
    }
}
