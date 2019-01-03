using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class CatalogToNuspecsProcessor : ICatalogEntriesProcessor
    {
        private readonly NuspecStore _downloader;
        private readonly ILogger<CatalogToNuspecsProcessor> _logger;

        public CatalogToNuspecsProcessor(NuspecStore downloader, ILogger<CatalogToNuspecsProcessor> logger)
        {
            _downloader = downloader;
            _logger = logger;
        }

        public string CursorName => CursorNames.CatalogToNuspecs;

        public IReadOnlyList<string> DependencyCursorNames => new List<string>
        {
             CursorNames.NuGetOrg.FlatContainer,
        };

        public async Task ProcessAsync(CatalogPageItem page, IReadOnlyList<CatalogLeafItem> leaves)
        {
            var packageIdentities = leaves
                .Select(x => new PackageIdentity(x.PackageId, x.ParsePackageVersion().ToNormalizedString()))
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
                    _logger.LogWarning("The .nuspec for package {Id} {Version} could not be found.", packageIdentity.Id, packageIdentity.Version);
                }
            }
        }
    }
}
