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
             CursorNames.NuGetOrg.FlatContainerGlobal,
             CursorNames.NuGetOrg.FlatContainerChina,
        };

        public async Task ProcessAsync(IReadOnlyList<CatalogEntry> entries)
        {
            var work = new ConcurrentBag<CatalogEntry>(entries);
            var tasks = Enumerable
                .Range(0, 32)
                .Select(i => DownloadNuspecAsync(work))
                .ToList();

            await Task.WhenAll(tasks);
        }

        private async Task DownloadNuspecAsync(ConcurrentBag<CatalogEntry> work)
        {
            await Task.Yield();

            CatalogEntry entry;
            while (work.TryTake(out entry))
            {
                var success = await _downloader.StoreNuspecAsync(
                    entry.Id,
                    entry.Version.ToNormalizedString(),
                    entry.NuspecUri.AbsoluteUri,
                    CancellationToken.None);

                if (!success)
                {
                    _log.LogWarning($"The .nuspec for package {entry.Id} {entry.Version} could not be found.");
                }
            }
        }
    }
}
