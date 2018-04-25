using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public delegate IQueryable<PackageEntity> GetPackages(EntityContext entities);

    public class PackageCommitEnumerator
    {
        public Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(
            GetPackages getPackages,
            DateTimeOffset start,
            DateTimeOffset end,
            int batchSize)
        {
            return GetPackageCommitsAsync(
                getPackages,
                start.UtcTicks,
                end.UtcTicks,
                batchSize,
                minimumPackages: 1);
        }

        private async Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(
            GetPackages getPackages,
            long start,
            long end,
            int batchSize,
            int minimumPackages)
        {
            var commits = new List<PackageCommit>();
            var totalFetched = 0;
            int fetched;
            do
            {
                using (var entities = new EntityContext())
                {
                    var packages = await getPackages(entities)
                        .Include(x => x.PackageRegistration)
                        .Include(x => x.CatalogPackage)
                        .Where(x => x.CatalogPackage != null)
                        .Where(x => x.CatalogPackage.LastCommitTimestamp > start && x.CatalogPackage.LastCommitTimestamp <= end)
                        .OrderBy(x => x.CatalogPackage.LastCommitTimestamp)
                        .Take(batchSize)
                        .ToListAsync();

                    fetched = packages.Count;
                    totalFetched += packages.Count;

                    var packageGroups = packages
                        .GroupBy(x => x.CatalogPackage.LastCommitTimestamp)
                        .ToDictionary(x => x.Key, x => x.ToList());

                    var commitTimestamps = packageGroups
                        .Keys
                        .OrderBy(x => x)
                        .ToList();

                    if (commitTimestamps.Count == 1 && fetched == batchSize)
                    {
                        // We've gotten a whole package but can't confidently move the start commit timestamp forward.
                        throw new InvalidOperationException(
                            "Only one commit timestamp was encountered. A large page size is required to proceed.");
                    }
                    else if (fetched < batchSize)
                    {
                        // We've reached the end and we have all of the last commit.
                        start = end;
                        commits.AddRange(InitializePackageCommits(
                            commitTimestamps,
                            packageGroups));
                    }
                    else if (fetched > 0)
                    {
                        // Ignore the last commit timestamp since we might have a partial commit.
                        commitTimestamps.RemoveAt(commitTimestamps.Count - 1);
                        start = commitTimestamps.Last();
                        commits.AddRange(InitializePackageCommits(
                            commitTimestamps,
                            packageGroups));
                    }
                }
            }
            while (fetched > 0 && start < end && totalFetched < minimumPackages);

            return commits;
        }

        private IEnumerable<PackageCommit> InitializePackageCommits(
            IEnumerable<long> commitTimestamps,
            Dictionary<long, List<PackageEntity>> packageGroups)
        {
            foreach (var commitTimestamp in commitTimestamps)
            {
                yield return new PackageCommit(
                    new DateTimeOffset(commitTimestamp, TimeSpan.Zero),
                    packageGroups[commitTimestamp]);
            }
        }
    }
}
