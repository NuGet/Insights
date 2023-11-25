// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class CatalogCommitTimestampProvider
    {
        private class Index
        {
            public Index(CatalogIndex index)
            {
                LatestCommit = index.CommitTimestamp;
                Pages = index
                    .Items
                    .OrderBy(x => x.CommitTimestamp)
                    .Select(x => new Page(x))
                    .ToList();
                PagesForSearch = new List<ILatestCommit>(Pages);
            }

            public DateTimeOffset LatestCommit { get; private set; }
            public List<ILatestCommit> PagesForSearch { get; }
            public List<Page> Pages { get; }

            public void Update(CatalogIndex index)
            {
                if (index.CommitTimestamp == LatestCommit)
                {
                    return;
                }

                var sortedItems = index.Items.OrderBy(x => x.CommitTimestamp).ToList();

                // Verify all but the last page have the same commit timestamp as before.
                for (var i = 0; i < Pages.Count - 1; i++)
                {
                    if (Pages[i].Url != sortedItems[i].Url)
                    {
                        throw new ArgumentException($"The page at index {i} has a URL '{sortedItems[i].Url}' that is different than before '{Pages[i].Url}'.", nameof(index));
                    }

                    if (Pages[i].LatestCommit != sortedItems[i].CommitTimestamp)
                    {
                        throw new ArgumentException($"The page at index {i} has a commit timestamp '{sortedItems[i].CommitTimestamp:O}' that is different than before '{Pages[i].LatestCommit:O}'.", nameof(index));
                    }
                }

                LatestCommit = index.CommitTimestamp;

                // Update the last item with the latest page data, and append more as needed.
                for (var i = Pages.Count - 1; i < sortedItems.Count; i++)
                {
                    if (i < Pages.Count)
                    {
                        Pages[i].Update(sortedItems[i]);
                    }
                    else
                    {
                        Pages.Add(new Page(sortedItems[i]));
                        PagesForSearch.Add(Pages.Last());
                    }
                }
            }
        }

        private interface ILatestCommit
        {
            DateTimeOffset LatestCommit { get; }
        }

        private class LatestCommitComparer : IComparer<ILatestCommit>
        {
            public static LatestCommitComparer Instance { get; } = new LatestCommitComparer();

            public int Compare(ILatestCommit? x, ILatestCommit? y)
            {
                return x!.LatestCommit.CompareTo(y!.LatestCommit);
            }
        }

        private class LatestCommitSearch : ILatestCommit
        {
            public LatestCommitSearch(DateTimeOffset latestCommit)
            {
                LatestCommit = latestCommit;
            }

            public DateTimeOffset LatestCommit { get; }
        }

        private class Page : ILatestCommit
        {
            public Page(CatalogPageItem item)
            {
                LatestCommit = item.CommitTimestamp;
                Url = item.Url;
                Commits = new List<DateTimeOffset>();
            }

            public DateTimeOffset LatestCommit { get; private set; }
            public string Url { get; }
            public List<DateTimeOffset> Commits { get; }

            public void Update(CatalogPageItem item)
            {
                if (item.Url != Url)
                {
                    throw new ArgumentException($"The provided catalog page item URL '{item.Url}' does not match the expected value '{Url}'.", nameof(item));
                }

                if (item.CommitTimestamp == LatestCommit)
                {
                    return;
                }

                Commits.Clear();
                LatestCommit = item.CommitTimestamp;
            }

            public void Update(CatalogPage page)
            {
                if (page.Url != Url)
                {
                    throw new ArgumentException($"The provided catalog page URL '{page.Url}' does not match the expected value '{Url}'.", nameof(page));
                }

                var sortedCommits = page
                    .Items
                    .Select(x => x.CommitTimestamp)
                    .Distinct()
                    .Order()
                    .ToList();

                if (sortedCommits.Last() != page.CommitTimestamp)
                {
                    throw new ArgumentException($"The provided page has a commit timestamp '{page.CommitTimestamp:O}' that is different than the highest value '{sortedCommits.Last():O}'.", nameof(page));
                }

                // Verify all existing commit timestamp still exist.
                for (var i = 0; i < Commits.Count; i++)
                {
                    if (Commits[i] != sortedCommits[i])
                    {
                        throw new ArgumentException($"The commit timestamp '{sortedCommits[i]:O}' at index {i} should not have a different value than before '{Commits[i]:O}'.", nameof(page));
                    }
                }

                LatestCommit = page.CommitTimestamp;
                Commits.AddRange(sortedCommits.Skip(Commits.Count));
            }
        }


        private readonly CatalogClient _catalogClient;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private Index? _index;

        public CatalogCommitTimestampProvider(CatalogClient catalogClient)
        {
            _catalogClient = catalogClient;
        }

        public async Task<DateTimeOffset?> GetNextAsync(DateTimeOffset min)
        {
            await _lock.WaitAsync();
            try
            {
                return await GetNextDefaultMaxInternalAsync(min, allowPageFetch: true);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<DateTimeOffset?> GetNextDefaultMaxInternalAsync(DateTimeOffset min, bool allowPageFetch)
        {
            if (_index == null)
            {
                _index = new Index(await _catalogClient.GetCatalogIndexAsync());
                allowPageFetch = false;
            }

            // Binary search returns a value greater than or equal to zero if the provided min exactly matches on of
            // the items in the list. We'll refer to this as an "exact match".
            var pageMatch = _index.PagesForSearch.BinarySearch(new LatestCommitSearch(min), LatestCommitComparer.Instance);
            Page page;

            if (pageMatch >= 0)
            {
                if (pageMatch < _index.Pages.Count - 1)
                {
                    // If the exact match on a page commit timestamp is not the last page, then we know exactly which
                    // page has the next timestamp. It's page directly after the exact match.
                    page = _index.Pages[pageMatch + 1];
                }
                else if (allowPageFetch)
                {
                    // If the exact match on a page commit timestamp is the last page, then we need to try to fetch more
                    // pages and then try the search again.
                    _index.Update(await _catalogClient.GetCatalogIndexAsync());
                    return await GetNextDefaultMaxInternalAsync(min, allowPageFetch: false);
                }
                else
                {
                    // If we've reached this point, that means there was an exact match on the last page and we've
                    // already tried to refresh the page commit timestamps and we again matched the last page. This
                    // means there are no commit timestamps higher than the provided min.
                    return null;
                }
            }
            else
            {
                var nextMatch = ~pageMatch;
                if (nextMatch == _index.Pages.Count)
                {
                    if (!allowPageFetch)
                    {
                        // If we've reached this point, that means that the provided min is greater than the last page
                        // and we've already tried to refresh the data. No greater commit timestamp exists at this time.
                        return null;
                    }

                    // If we've reached this point, that means that the provided min is greater than the last page and
                    // we need to try to fetch more pages and try the search again.
                    _index.Update(await _catalogClient.GetCatalogIndexAsync());
                    return await GetNextDefaultMaxInternalAsync(min, allowPageFetch: false);
                }
                else
                {
                    // If we've reached this point, that means we've identified the page with the lowest commit
                    // timestamp that exceeds the provided min. This is the page that will have the next highest
                    // specific commit timestamp.
                    page = _index.Pages[nextMatch];
                }
            }

            // If we've reached this point, we've identified a page that needs to be checked for the very next highest
            // commit timestamp.
            if (page.Commits.Count == 0)
            {
                page.Update(await _catalogClient.GetCatalogPageAsync(page.Url));
            }

            var commitMatch = page.Commits.BinarySearch(min);
            if (commitMatch >= 0)
            {
                return page.Commits[commitMatch + 1];
            }
            else
            {
                return page.Commits[~commitMatch];
            }
        }
    }
}
