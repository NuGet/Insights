using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogService
    {
        private readonly ILogger _log;

        public CatalogService(ILogger log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task AddOrUpdateAsync(
            CatalogPageEntry page,
            IReadOnlyList<CatalogEntry> leaves,
            IReadOnlyDictionary<string, long> identityToPackageKey)
        {
            _log.LogInformation($"Adding or updating catalog page {page.Uri.OriginalString}.");
            using (var context = new EntityContext())
            {
                var pageUrl = page.Uri.OriginalString;
                var existing = await context
                    .CatalogPages
                    .Include(x => x.CatalogCommits)
                    .ThenInclude(x => x.CatalogLeaves)
                    .Where(x => x.Url == pageUrl)
                    .FirstOrDefaultAsync();

                var latest = Initialize(pageUrl, leaves, identityToPackageKey);

                Merge(context, existing, latest);

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await context.SaveChangesAsync();
                _log.LogInformation($"Committed {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
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
                }
            }
        }

        private CatalogPageEntity Initialize(
            string pageUrl,
            IReadOnlyList<CatalogEntry> leaves,
            IReadOnlyDictionary<string, long> identityToPackageKey)
        {
            var pageEntity = new CatalogPageEntity
            {
                Url = pageUrl,
                CatalogCommits = new List<CatalogCommitEntity>(),
            };

            var commits = leaves
                .GroupBy(x => new
                {
                    CommitTimestamp = x.CommitTimeStamp.ToUniversalTime(),
                    CommitId = x.CommitId,
                });

            var uniqueCommitTimestamps = new HashSet<DateTimeOffset>();
            var uniqueCommitIds = new HashSet<Guid>();

            foreach (var commit in commits)
            {
                if (!uniqueCommitTimestamps.Add(commit.Key.CommitTimestamp))
                {
                    throw new InvalidOperationException("A duplicate commit timestamp was found.");
                }

                if (!uniqueCommitIds.Add(Guid.Parse(commit.Key.CommitId)))
                {
                    throw new InvalidOperationException("A duplicate commit ID was found.");
                }

                var commitLeaves = commit.ToList();

                var commitEntity = new CatalogCommitEntity
                {
                    CatalogPage = pageEntity,
                    CommitId = commit.Key.CommitId,
                    CommitTimestamp = commit.Key.CommitTimestamp.UtcTicks,
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

                    commitEntity.CatalogLeaves.Add(leafEntity);
                }
            }

            return pageEntity;
        }
    }
}
