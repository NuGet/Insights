using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageCommitEnumerator
    {
        private const int PageSize = 5000;
        
        public Task<IReadOnlyList<PackageCommit>> GetPackageBatchesAsync(
            DateTimeOffset start,
            DateTimeOffset end)
        {
            return GetPackageBatchesAsync(start.UtcTicks, end.UtcTicks, minimumPackages: 1);
        }

        public Task<IReadOnlyList<PackageCommit>> GetAllPackageBatchesAsync(
            DateTimeOffset start,
            DateTimeOffset end)
        {
            return GetPackageBatchesAsync(start.UtcTicks, end.UtcTicks, minimumPackages: int.MaxValue);
        }

        private Task<IReadOnlyList<PackageCommit>> GetPackageBatchesAsync(
            DateTimeOffset start,
            DateTimeOffset end,
            int minimumPackages)
        {
            return GetPackageBatchesAsync(start.UtcTicks, end.UtcTicks, minimumPackages);
        }

        private async Task<IReadOnlyList<PackageCommit>> GetPackageBatchesAsync(long start, long end, int minimumPackages)
        {
            var commits = new List<PackageCommit>();
            var totalFetched = 0;
            int fetched;
            do
            {
                using (var entities = new EntityContext())
                {
                    var packages = await entities
                        .Packages
                        .Where(x => x.LastCommitTimestamp > start && x.LastCommitTimestamp <= end)
                        .OrderBy(x => x.LastCommitTimestamp)
                        .Take(PageSize)
                        .ToListAsync();

                    fetched = packages.Count;
                    totalFetched += packages.Count;

                    var packageGroups = packages
                        .GroupBy(x => x.LastCommitTimestamp)
                        .ToDictionary(x => x.Key, x => x.ToList());

                    var commitTimestamps = packageGroups
                        .Keys
                        .OrderBy(x => x)
                        .ToList();

                    if (commitTimestamps.Count == 1 && fetched == PageSize)
                    {
                        // We've gotten a whole package but can't confidently move the start commit timestamp forward.
                        throw new InvalidOperationException(
                            "Only one commit timestamp was encountered. A large page size is required to proceed.");
                    }
                    else if (fetched < PageSize)
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
            Dictionary<long, List<Package>> packageGroups)
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
