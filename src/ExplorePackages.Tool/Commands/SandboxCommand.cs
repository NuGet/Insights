using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Logic.Worker.RunRealRestore;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly CatalogScanService _catalogScanService;
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly CursorStorageService _cursorStorageService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly LatestPackageLeafStorageService _latestPackageLeafStorageService;
        private readonly AppendResultStorageService _findPackageAssetsStorageService;
        private readonly MessageEnqueuer _messageEnqueuer;

        public SandboxCommand(
            CatalogScanService catalogScanService,
            IWorkerQueueFactory workerQueueFactory,
            CursorStorageService cursorStorageService,
            CatalogScanStorageService catalogScanStorageService,
            LatestPackageLeafStorageService latestPackageLeafStorageService,
            AppendResultStorageService findPackageAssetsStorageService,
            MessageEnqueuer messageEnqueuer)
        {
            _catalogScanService = catalogScanService;
            _workerQueueFactory = workerQueueFactory;
            _cursorStorageService = cursorStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _latestPackageLeafStorageService = latestPackageLeafStorageService;
            _findPackageAssetsStorageService = findPackageAssetsStorageService;
            _messageEnqueuer = messageEnqueuer;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _workerQueueFactory.GetQueue().CreateIfNotExistsAsync(retry: true);
            await _cursorStorageService.InitializeAsync();
            await _catalogScanStorageService.InitializeAsync();
            await _latestPackageLeafStorageService.InitializeAsync();
            await _findPackageAssetsStorageService.InitializeAsync(FindPackageAssetsConstants.ContainerName);
            await _findPackageAssetsStorageService.InitializeAsync(RunRealRestoreConstants.ContainerName);

            // await _catalogScanService.UpdateGetPackageAssets();
            await EnqueueRunRealRestoreAsync();
        }

        private async Task EnqueueRunRealRestoreAsync()
        {
            var frameworks = new[]
            {
                ".NETCoreApp,Version=v1.1",
                ".NETCoreApp,Version=v2.0",
                ".NETCoreApp,Version=v2.1",
                ".NETCoreApp,Version=v2.2",
                ".NETCoreApp,Version=v3.0",
                ".NETCoreApp,Version=v3.1",
                ".NETCoreApp,Version=v5.0",
                ".NETFramework,Version=v2.0",
                ".NETFramework,Version=v3.5",
                ".NETFramework,Version=v4.0",
                ".NETFramework,Version=v4.5.1",
                ".NETFramework,Version=v4.5.2",
                ".NETFramework,Version=v4.5",
                ".NETFramework,Version=v4.6.1",
                ".NETFramework,Version=v4.6.2",
                ".NETFramework,Version=v4.6",
                ".NETFramework,Version=v4.7.1",
                ".NETFramework,Version=v4.7.2",
                ".NETFramework,Version=v4.7",
                ".NETFramework,Version=v4.8",
                ".NETStandard,Version=v1.0",
                ".NETStandard,Version=v1.1",
                ".NETStandard,Version=v1.3",
                ".NETStandard,Version=v1.4",
                ".NETStandard,Version=v1.5",
                ".NETStandard,Version=v1.6",
                ".NETStandard,Version=v2.0",
            }
                .Select(x => NuGetFramework.Parse(x))
                .ToList();

            var source = "https://api.nuget.org/v3/index.json";
            var repository = Repository.Factory.GetCoreV3(source);
            var search = await repository.GetResourceAsync<PackageSearchResource>();
            var logger = NullLogger.Instance;
            var cancellationToken = CancellationToken.None;

            var take = 1000;
            Console.Write($"Searching for top {take} packages...");
            var results = await search.SearchAsync(
                searchTerm: string.Empty,
                new SearchFilter(includePrerelease: true),
                skip: 0,
                take: take,
                log: logger,
                cancellationToken: cancellationToken);
            Console.WriteLine(" done.");

            Console.WriteLine("Enqueueing messages...");
            var messages = results
                .SelectMany(p => frameworks
                    .Take(take)
                    .Select(f => new { Framework = f, Package = p.Identity }))
                .Select(m => new RunRealRestoreMessage
                {
                    Id = m.Package.Id,
                    Version = m.Package.Version.ToNormalizedString(),
                    Framework = m.Framework.GetShortFolderName(),
                })
                .ToList();
            await _messageEnqueuer.EnqueueAsync(messages);
            Console.WriteLine("Done.");
        }

        public bool IsInitializationRequired() => false;
        public bool IsDatabaseRequired() => false;
        public bool IsSingleton() => false;

    }
}
