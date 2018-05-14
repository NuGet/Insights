using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.CatalogReader;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogService
    {
        private static readonly Regex PagePathPattern = new Regex(@"page(?<PageIndex>[0-9]|[1-9][0-9]+)\.json");

        private readonly ServiceIndexCache _serviceIndexCache;
        private readonly ILogger<CatalogService> _logger;

        public CatalogService(
            ServiceIndexCache serviceIndexCache,
            ILogger<CatalogService> logger)
        {
            _serviceIndexCache = serviceIndexCache ?? throw new ArgumentNullException(nameof(serviceIndexCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task AddOrUpdateAsync(
            CatalogPageEntry page,
            IReadOnlyList<CatalogEntry> leaves,
            IReadOnlyDictionary<string, long> identityToPackageKey)
        {
            _logger.LogInformation("Adding or updating catalog page {PageUri}.", page.Uri.OriginalString);
            using (var context = new EntityContext())
            {
                var pageUrl = page.Uri.OriginalString;
                var existing = await context
                    .CatalogPages
                    .Include(x => x.CatalogCommits)
                    .ThenInclude(x => x.CatalogLeaves)
                    .Where(x => x.Url == pageUrl)
                    .FirstOrDefaultAsync();

                var latest = await InitializeAsync(pageUrl, leaves, identityToPackageKey);

                Merge(context, existing, latest);

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await context.SaveChangesAsync();
                _logger.LogInformation("Committed {Changes} changes. {ElapsedMilliseconds}ms", changes, commitStopwatch.ElapsedMilliseconds);
            }
        }

        private void Merge(EntityContext context, CatalogPageEntity existing, CatalogPageEntity latest)
        {
            if (existing == null)
            {
                context.CatalogPages.Add(latest);
                return;
            }

            var commitIdToCommit = existing
                .CatalogCommits
                .ToDictionary(x => x.CommitId);

            foreach (var latestCommit in latest.CatalogCommits)
            {
                if (!commitIdToCommit.TryGetValue(latestCommit.CommitId, out var existingCommit))
                {
                    latestCommit.CatalogPage = existing;
                    context.CatalogCommits.Add(latestCommit);
                    continue;
                }

                if (latestCommit.Count != existingCommit.Count
                    || latestCommit.CatalogLeaves.Count != existingCommit.CatalogLeaves.Count)
                {
                    throw new InvalidOperationException("The number of catalog leaves cannot change in a commit.");
                }

                var packageKeyToLeaf = existingCommit
                    .CatalogLeaves
                    .ToDictionary(x => x.PackageKey);

                foreach (var latestLeaf in latestCommit.CatalogLeaves)
                {
                    if (!packageKeyToLeaf.TryGetValue(latestLeaf.PackageKey, out var existingLeaf))
                    {
                        throw new InvalidOperationException("The packages in a commit cannot change.");
                    }

                    if (latestLeaf.Type != existingLeaf.Type)
                    {
                        throw new InvalidOperationException("The type of a catalog leaf cannot change.");
                    }

                    existingLeaf.RelativePath = latestLeaf.RelativePath;
                }
            }
        }

        private async Task<CatalogPageEntity> InitializeAsync(
            string pageUrl,
            IReadOnlyList<CatalogEntry> leaves,
            IReadOnlyDictionary<string, long> identityToPackageKey)
        {
            await VerifyExpectedPageUrlAsync(pageUrl);

            var pageEntity = new CatalogPageEntity
            {
                Url = pageUrl,
                CatalogCommits = new List<CatalogCommitEntity>(),
            };

            var commits = leaves
                .GroupBy(x => x.CommitTimeStamp.ToUniversalTime());

            foreach (var commit in commits)
            {
                var commitLeaves = commit.ToList();

                // This really only every be one, but there is an oddball:
                // https://api.nuget.org/v3/catalog0/page868.json, timestamp: 2015-04-17T23:24:26.0796162Z
                var commitId = string.Join(" ", commit
                    .Select(x => Guid.Parse(x.CommitId))
                    .OrderBy(x => x)
                    .Distinct()
                    .ToList());

                var commitEntity = new CatalogCommitEntity
                {
                    CatalogPage = pageEntity,
                    CommitId = commitId,
                    CommitTimestamp = commit.Key.UtcTicks,
                    CatalogLeaves = new List<CatalogLeafEntity>(),
                    Count = commitLeaves.Count,
                };

                pageEntity.CatalogCommits.Add(commitEntity);

                foreach (var leaf in commitLeaves)
                {
                    var identity = $"{leaf.Id}/{leaf.Version.ToNormalizedString()}";
                    var packageKey = identityToPackageKey[identity];

                    if (leaf.Types.Count != 1)
                    {
                        throw new InvalidOperationException($"Found a catalog leaf with {leaf.Types.Count} types instead of 1.");
                    }

                    CatalogLeafType type;
                    if (leaf.IsAddOrUpdate)
                    {
                        type = CatalogLeafType.PackageDetails;
                    }
                    else if (leaf.IsDelete)
                    {
                        type = CatalogLeafType.PackageDelete;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unexpected catalog leaf type.");
                    }

                    var leafEntity = new CatalogLeafEntity
                    {
                        CatalogCommit = commitEntity,
                        PackageKey = packageKey,
                        Type = type,
                    };

                    await VerifyExpectedLeafUrlAsync(leaf, leafEntity);

                    commitEntity.CatalogLeaves.Add(leafEntity);
                }
            }

            return pageEntity;
        }

        private async Task VerifyExpectedPageUrlAsync(string pageUrl)
        {
            var catalogBaseUrl = await GetCatalogBaseUrlAsync();

            if (!pageUrl.StartsWith(catalogBaseUrl))
            {
                throw new InvalidOperationException("The catalog page URL should start with the catalog base URL.");
            }

            var path = pageUrl.Substring(catalogBaseUrl.Length);

            if (!PagePathPattern.IsMatch(path))
            {
                throw new InvalidOperationException("The catalog page URL relative path does not have the expected pattern.");
            }
        }

        private async Task VerifyExpectedLeafUrlAsync(CatalogEntry entry, CatalogLeafEntity leafEntity)
        {
            var entryUrl = entry.Uri.OriginalString;
            var catalogBaseUrl = await GetCatalogBaseUrlAsync();

            if (!entryUrl.StartsWith(catalogBaseUrl))
            {
                throw new InvalidOperationException("The catalog leaf URL should start with the catalog base URL.");
            }

            var actualPath = entryUrl.Substring(catalogBaseUrl.Length);
            var expectedPath = string.Join("/", new[]
            {
                "data",
                entry.CommitTimeStamp.ToString("yyyy.MM.dd.HH.mm.ss"),
                $"{Uri.EscapeDataString(entry.Id.ToLowerInvariant())}.{entry.Version.ToNormalizedString().ToLowerInvariant()}.json",
            });

            // This should always be true, but we have an oddball:
            // https://api.nuget.org/v3/catalog0/page848.json
            // https://api.nuget.org/v3/catalog0/data/2015.04.03.23.35.56/xunit.core.2.0.0.json
            // Timestamp is 2015-04-03T23:14:16.0340591Z
            if (actualPath != expectedPath)
            {
                leafEntity.RelativePath = actualPath;
            }
            else
            {
                leafEntity.RelativePath = null;
            }
        }

        private async Task<string> GetCatalogBaseUrlAsync()
        {
            var catalogIndexUrl = await _serviceIndexCache.GetUrlAsync(ServiceIndexTypes.Catalog);
            var lastSlashIndex = catalogIndexUrl.LastIndexOf('/');
            if (lastSlashIndex < 0)
            {
                throw new InvalidOperationException("No slashes were found in the catalog index URL.");
            }

            var catalogBaseUrl = catalogIndexUrl.Substring(0, lastSlashIndex + 1);

            var indexPath = catalogIndexUrl.Substring(catalogBaseUrl.Length);
            if (indexPath != "index.json")
            {
                throw new InvalidOperationException("The catalog index does not have the expected relative path.");
            }

            return catalogBaseUrl;
        }
    }
}
