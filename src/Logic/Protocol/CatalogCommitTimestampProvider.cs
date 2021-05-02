using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class CatalogCommitTimestampProvider
    {
        private readonly CatalogClient _catalogClient;
        private readonly SemaphoreSlim _commitTimestampsLock = new SemaphoreSlim(1);
        private CatalogIndex _index;
        private IReadOnlyList<DateTimeOffset> _pageCommits;

        public CatalogCommitTimestampProvider(CatalogClient catalogClient)
        {
            _catalogClient = catalogClient;
        }

        public async Task<DateTimeOffset?> GetNextAsync(DateTimeOffset min)
        {
            await _commitTimestampsLock.WaitAsync();
            try
            {
                return await GetNextDefaultMaxInternalAsync(min, allowRetry: true);
            }
            finally
            {
                _commitTimestampsLock.Release();
            }
        }

        private async Task<DateTimeOffset?> GetNextDefaultMaxInternalAsync(DateTimeOffset min, bool allowRetry)
        {

            // Try to use the currently cached page first. This works well if we get close, successive commit
            // timestamps passed into this method.
            var pageCommitsNext = ReadPageCommits(min);
            if (pageCommitsNext.HasValue)
            {
                return pageCommitsNext;
            }

            if (_index == null)
            {
                _index = await _catalogClient.GetCatalogIndexAsync();
            }

            // If the latest index is stale (meaning it is older than the provided timestamp), clear it and try again.
            if (_index.CommitTimestamp <= min)
            {
                _index = null;
                if (allowRetry)
                {
                    return await GetNextDefaultMaxInternalAsync(min, allowRetry: false);
                }
                else
                {
                    return null;
                }
            }

            // Find the next applicable page within the index and cache it.
            var nextPageItem = _index.GetPagesInBounds(min, DateTimeOffset.MaxValue).First();
            var nextPage = await _catalogClient.GetCatalogPageAsync(nextPageItem.Url);
            _pageCommits = nextPage
                .Items
                .Select(x => x.CommitTimestamp)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
            return ReadPageCommits(min).Value;
        }

        private DateTimeOffset? ReadPageCommits(DateTimeOffset min)
        {
            if (_pageCommits != null)
            {
                var next = _pageCommits.SkipWhile(x => x <= min).ToList();
                if (next.Count > 0)
                {
                    return next[0];
                }

                _pageCommits = null;
            }

            return null;
        }
    }
}
