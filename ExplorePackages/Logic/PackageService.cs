using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.CatalogReader;
using NuGet.Common;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageService
    {
        private readonly PackageCommitEnumerator _enumerator;
        private readonly ILogger _log;

        public PackageService(ILogger log)
        {
            _enumerator = new PackageCommitEnumerator();
            _log = log;
        }

        public async Task<Package> GetPackageAsync(string id, string version)
        {
            var normalizedVersion = NuGetVersion.Parse(version).ToNormalizedString();
            using (var entityContext = new EntityContext())
            {
                return await entityContext
                    .Packages
                    .Where(x => x.Id == id && x.Version == normalizedVersion)
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<IReadOnlyList<Package>> GetBatchAsync(IReadOnlyList<PackageIdentity> identities)
        {
            using (var entityContext = new EntityContext())
            {
                var identityStrings = identities
                    .Select(x => $"{x.Id}/{x.Version}")
                    .ToList();

                return await entityContext
                    .Packages
                    .Where(x => identityStrings.Contains(x.Identity))
                    .ToListAsync();
            }
        }

        public Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(
            DateTimeOffset start,
            DateTimeOffset end)
        {
            return _enumerator.GetPackageCommitsAsync(start, end, minimumPackages: 1);
        }

        public Task<IReadOnlyList<PackageCommit>> GetAllPackageCommitsAsync(
            DateTimeOffset start,
            DateTimeOffset end)
        {
            return _enumerator.GetPackageCommitsAsync(start, end, minimumPackages: int.MaxValue);
        }

        /// <summary>
        /// Adds the provided catalog entries to the database. Catalog entries are processed in the order provided.
        /// </summary>
        public async Task AddOrUpdateBatchAsync(IEnumerable<CatalogEntry> entries)
        {
            using (var entityContext = new EntityContext())
            {
                // Make sure to process the entries in chronological order.
                var sortedEntries = entries
                    .OrderBy(x => x.CommitTimeStamp);

                // Create a mapping from package ID + "/" + package version to package item.
                var identityToLatest = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in sortedEntries)
                {
                    var identity = $"{entry.Id}/{entry.Version}";

                    Package latestPackage;
                    if (!identityToLatest.TryGetValue(identity, out latestPackage))
                    {
                        latestPackage = new Package
                        {
                            Id = entry.Id,
                            Version = entry.Version.ToNormalizedString(),
                            Identity = identity,
                            Deleted = entry.IsDelete,
                            FirstCommitTimestamp = entry.CommitTimeStamp.UtcTicks,
                            LastCommitTimestamp = entry.CommitTimeStamp.UtcTicks,
                        };
                        identityToLatest[latestPackage.Identity] = latestPackage;
                    }
                    else
                    {
                        latestPackage.Deleted = entry.IsDelete;

                        latestPackage.FirstCommitTimestamp = Math.Min(
                            latestPackage.FirstCommitTimestamp,
                            entry.CommitTimeStamp.UtcTicks);

                        latestPackage.LastCommitTimestamp = Math.Max(
                            latestPackage.LastCommitTimestamp,
                            entry.CommitTimeStamp.UtcTicks);
                    }
                }

                var getExistingStopwatch = Stopwatch.StartNew();
                var identities = identityToLatest.Keys.ToList();
                var existingPackages = await entityContext
                    .Packages
                    .Where(p => identities.Contains(p.Identity))
                    .ToListAsync();

                _log.LogInformation($"Got {existingPackages.Count} existing. {getExistingStopwatch.ElapsedMilliseconds}ms");

                // Update existing records.
                foreach (var existingPackage in existingPackages)
                {
                    var latestPackage = identityToLatest[existingPackage.Identity];
                    identityToLatest.Remove(existingPackage.Identity);

                    existingPackage.Deleted = latestPackage.Deleted;

                    existingPackage.FirstCommitTimestamp = Math.Min(
                        existingPackage.FirstCommitTimestamp,
                        latestPackage.FirstCommitTimestamp);

                    existingPackage.LastCommitTimestamp = Math.Max(
                        existingPackage.LastCommitTimestamp,
                        latestPackage.LastCommitTimestamp);
                }

                // Add new records.
                foreach (var pair in identityToLatest)
                {
                    entityContext.Packages.Add(pair.Value);
                }

                var commitStopwatch = Stopwatch.StartNew();
                var changes = await entityContext.SaveChangesAsync();
                _log.LogInformation($"Committed {changes} changes. {commitStopwatch.ElapsedMilliseconds}ms");
            }
        }

        public class PackageCommitEnumerator
        {
            private const int PageSize = 2000;
            
            public Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(
                DateTimeOffset start,
                DateTimeOffset end,
                int minimumPackages)
            {
                return GetPackageCommitsAsync(start.UtcTicks, end.UtcTicks, minimumPackages);
            }

            private async Task<IReadOnlyList<PackageCommit>> GetPackageCommitsAsync(long start, long end, int minimumPackages)
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
}
