using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageQueryProcessor
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly PackageQueryService _queryService;
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

        public PackageQueryProcessor(
            PackagePathProvider pathProvider,
            IEnumerable<IPackageQuery> queries,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _queryService = new PackageQueryService(log);
            _queries = queries.ToList();
            _log = log;
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var cursorService = new CursorService();

            var cursorStarts = new Dictionary<string, DateTimeOffset>();
            var start = await GetMinimumQueryStartAsync(cursorService, cursorStarts);
            var end = await cursorService.GetMinimumAsync(new[]
{
                CursorNames.CatalogToDatabase,
                CursorNames.CatalogToNuspecs,
            });

            var complete = 0;
            var stopwatch = Stopwatch.StartNew();

            int commitCount;
            var packageService = new PackageService(_log);

            do
            {
                var commits = await packageService.GetPackageCommitsAsync(start, end);
                commitCount = commits.Count;

                var allQueryMatches = _queries.ToDictionary(
                    x => x.Name,
                    x => new List<PackageIdentity>());

                foreach (var commit in commits)
                {
                    foreach (var package in commit.Packages)
                    {
                        var context = GetPackageQueryContext(package);

                        foreach (var query in _queries)
                        {
                            if (commit.CommitTimestamp <= cursorStarts[query.CursorName])
                            {
                                continue;
                            }
                            
                            var isMatch = false;
                            try
                            {
                                isMatch = await query.IsMatchAsync(context);
                            }
                            catch (Exception e)
                            {
                                _log.LogError($"Query failure {query.Name}: {context.Package.Id} {context.Package.Version}"
                                    + Environment.NewLine
                                    + "  "
                                    + e.Message);
                                throw;
                            }

                            if (isMatch)
                            {
                                _log.LogInformation($"Query match {query.Name}: {package.Id} {package.Version}");
                                allQueryMatches[query.Name].Add(new PackageIdentity(package.Id, package.Version));
                            }
                        }

                        start = commit.CommitTimestamp;
                    }
                }

                complete += commits.Sum(x => x.Packages.Count);
                _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second). Cursors moving to {start:O}.");

                await PersistResultsAndCursorsAsync(cursorService, cursorStarts, start, allQueryMatches);
            }
            while (commitCount > 0);
        }

        private async Task<DateTimeOffset> GetMinimumQueryStartAsync(
            CursorService cursorService,
            Dictionary<string, DateTimeOffset> cursorStarts)
        {
            var start = DateTimeOffset.MaxValue;
            foreach (var query in _queries)
            {
                if (!cursorStarts.ContainsKey(query.CursorName))
                {
                    var cursorStart = await cursorService.GetAsync(query.CursorName);
                    cursorStarts[query.CursorName] = cursorStart;

                    if (cursorStart < start)
                    {
                        start = cursorStart;
                    }
                }
            }

            return start;
        }

        private async Task PersistResultsAndCursorsAsync(
            CursorService cursorService,
            Dictionary<string, DateTimeOffset> cursorStarts,
            DateTimeOffset start,
            Dictionary<string, List<PackageIdentity>> allQueryMatches)
        {
            foreach (var query in _queries)
            {
                if (allQueryMatches[query.Name].Any())
                {
                    await _queryService.AddQueryAsync(query.Name, query.CursorName);
                    await _queryService.AddMatchesAsync(query.Name, allQueryMatches[query.Name]);
                }

                if (cursorStarts[query.CursorName] < start)
                {
                    await cursorService.SetAsync(query.CursorName, start);
                    cursorStarts[query.CursorName] = start;
                }
            }
        }

        private PackageQueryContext GetPackageQueryContext(Package package)
        {
            var immutablePackage = new ImmutablePackage(package);
            var nuspecQueryContext = GetNuspecQueryContext(package);
            var isSemVer2 = NuspecUtility.IsSemVer2(nuspecQueryContext.Document);

            return new PackageQueryContext(immutablePackage, nuspecQueryContext, isSemVer2);
        }

        private NuspecQueryContext GetNuspecQueryContext(Package package)
        {
            var path = _pathProvider.GetLatestNuspecPath(package.Id, package.Version);
            var exists = false;
            XDocument document = null;
            try
            {
                if (File.Exists(path))
                {
                    exists = true;
                    using (var stream = File.OpenRead(path))
                    {
                        document = NuspecUtility.LoadXml(stream);
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Could not parse .nuspec for {package.Id} {package.Version}: {path}"
                    + Environment.NewLine
                    + "  "
                    + e.Message);

                throw;
            }

            if (!exists && !package.Deleted)
            {
                _log.LogWarning($"Could not find .nuspec for {package.Id} {package.Version}: {path}");
            }

            return new NuspecQueryContext(path, exists, document);
        }
    }
}
