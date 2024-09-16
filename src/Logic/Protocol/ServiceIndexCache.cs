// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol;

#nullable enable

namespace NuGet.Insights
{
    public class ServiceIndexCache
    {
        private const string TypeToUrlsKey = $"{nameof(ServiceIndexCache)}.TypeToUrls";

        private readonly Func<HttpClient> _httpClientFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ILogger<ServiceIndexCache> _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public ServiceIndexCache(
            Func<HttpClient> httpClientFactory,
            IMemoryCache memoryCache,
            IOptions<NuGetInsightsSettings> options,
            ILogger<ServiceIndexCache> logger)
        {
            _httpClientFactory = httpClientFactory;
            _memoryCache = memoryCache;
            _options = options;
            _logger = logger;
        }

        private async Task<ServiceIndexResourceV3> GetServiceIndexAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var attemptCount = 0;
                var elapsed = Stopwatch.StartNew();
                var maxAttemptDuration = TimeSpan.FromMinutes(1);
                while (true)
                {
                    try
                    {
                        attemptCount++;
                        var requestTime = DateTime.UtcNow;
                        var httpClient = _httpClientFactory();
                        var serviceIndexJson = await httpClient.ProcessResponseWithRetriesAsync(
                            () => new HttpRequestMessage(HttpMethod.Get, _options.Value.V3ServiceIndex),
                            async response =>
                            {
                                response.EnsureSuccessStatusCode();

                                using var stream = await response.Content.ReadAsStreamAsync(CancellationToken.None);
                                using var streamReader = new StreamReader(stream);
                                using var jsonReader = new JsonTextReader(streamReader);

                                return JObject.Load(jsonReader);
                            },
                            _logger,
                            CancellationToken.None);
                        return new ServiceIndexResourceV3(serviceIndexJson, requestTime);
                    }
                    catch (Exception ex) when (elapsed.Elapsed < maxAttemptDuration)
                    {
                        var sleepDuration = StorageUtility.GetMessageDelay(attemptCount);
                        _logger.LogTransientWarning(
                            ex,
                            "Failed to fetch and cache the V3 service index on attempt {AttemptCount}. Trying again in {SleepDuration}.",
                            attemptCount,
                            sleepDuration);
                        await Task.Delay(sleepDuration);
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IReadOnlyList<string>> GetUrlsAsync(string type)
        {
            var typeToUrls = await _memoryCache.GetOrCreateAsync(
                TypeToUrlsKey,
                async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);

                    var serviceIndex = await GetServiceIndexAsync();

                    IReadOnlyDictionary<string, IReadOnlyList<string>> typeToUrls = serviceIndex
                        .Entries
                        .GroupBy(entry => entry.Type)
                        .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Uri.AbsoluteUri).ToList());

                    return typeToUrls;
                });

            if (typeToUrls is null || !typeToUrls.TryGetValue(type, out var urls))
            {
                return [];
            }

            return urls;
        }

        public async Task<string> GetUrlAsync(string type)
        {
            var urls = await GetUrlsAsync(type);
            if (urls.Count == 0)
            {
                throw new InvalidOperationException($"No URL was found in the service index for type '{type}'.");
            }

            return urls[0];
        }
    }
}
