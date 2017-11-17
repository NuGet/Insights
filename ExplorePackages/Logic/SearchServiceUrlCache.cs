using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    /// <summary>
    /// This is a type separate from <see cref="SearchServiceUrlDiscoverer" /> so that the minimal amount of code can
    /// be included in a singleton.
    /// </summary>
    public class SearchServiceUrlCache : ISearchServiceUrlCacheInvalidator
    {
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private readonly object _lock = new object();
        private readonly Dictionary<CacheKey, CacheEntry> _cache = new Dictionary<CacheKey, CacheEntry>();

        public void InvalidateCache()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        public IReadOnlyList<string> GetUrls(string serviceIndexType, bool specificInstances)
        {
            var key = new CacheKey(serviceIndexType, specificInstances);
            lock (_lock)
            {
                if (!_cache.TryGetValue(key, out var entry))
                {
                    return null;
                }

                if (DateTimeOffset.UtcNow - entry.Timestamp > _cacheDuration)
                {
                    _cache.Remove(key);
                    return null;
                }

                return entry.Urls;
            }
        }

        public void SetUrls(string serviceIndexType, bool specificInstances, IReadOnlyList<string> urls)
        {
            var key = new CacheKey(serviceIndexType, specificInstances);
            lock (_lock)
            {
                _cache[key] = new CacheEntry(urls);
            }
        }

        private class CacheEntry
        {
            public CacheEntry(IReadOnlyList<string> urls)
            {
                Urls = urls;
                Timestamp = DateTimeOffset.UtcNow;
            }

            public IReadOnlyList<string> Urls { get; }
            public DateTimeOffset Timestamp { get; }
        }

        private class CacheKey : IEquatable<CacheKey>
        {
            private readonly int _hashCode;

            public CacheKey(string serviceIndexType, bool specificInstances)
            {
                ServiceIndexType = serviceIndexType;
                SpecificInstances = specificInstances;
                _hashCode = $"{serviceIndexType}{specificInstances}".GetHashCode();
            }

            public string ServiceIndexType { get; }
            public bool SpecificInstances { get; }

            public bool Equals(CacheKey other)
            {
                return ServiceIndexType == other.ServiceIndexType
                    && SpecificInstances == other.SpecificInstances;
            }

            public override int GetHashCode()
            {
                return _hashCode;
            }
        }
    }
}
