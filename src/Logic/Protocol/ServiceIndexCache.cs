// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

#nullable enable

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
            var url = urls.FirstOrDefault();
            if (url is null)
            {
                throw new InvalidOperationException($"No URL was found in the service index for type '{type}'.");
            }

            return url;
        }
    }
}
