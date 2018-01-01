using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.CatalogReader;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToNuspecsProcessor : ICatalogEntriesProcessor
    {
        private readonly NuspecDownloader _downloader;
        private readonly ILogger _log;

        public CatalogToNuspecsProcessor(NuspecDownloader downloader, ILogger log)
        {
            _downloader = downloader;
            _log = log;
        }

        public string CursorName => CursorNames.CatalogToNuspecs;

        public IReadOnlyList<string> DependencyCursorNames => new List<string>
        {
             CursorNames.NuGetOrg.FlatContainer,
        };

        public async Task ProcessAsync(IReadOnlyList<CatalogEntry> entries)
        {
            var packageIdentities = entries
                .Select(x => new PackageIdentity(x.Id, x.Version.ToNormalizedString()))
                .Distinct()
                .ToList();

            var work = new ConcurrentBag<PackageIdentity>(packageIdentities);

            var tasks = Enumerable
                .Range(0, 32)
                .Select(i => DownloadNuspecAsync(work))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task DownloadNuspecAsync(ConcurrentBag<PackageIdentity> work)
        {
            while (work.TryTake(out var packageIdentity))
            {
                var success = await _downloader.StoreNuspecAsync(
                    packageIdentity.Id,
                    packageIdentity.Version,
                    CancellationToken.None);

                if (!success)
                {
                    _log.LogWarning($"The .nuspec for package {packageIdentity.Id} {packageIdentity.Version} could not be found.");
                }
            }
        }
    }
}
