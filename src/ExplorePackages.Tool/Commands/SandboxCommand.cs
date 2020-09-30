using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Knapcode.ExplorePackages.Logic.Worker.BlobStorage;
using Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets;
using McMaster.Extensions.CommandLineUtils;

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

        public SandboxCommand(
            CatalogScanService catalogScanService,
            IWorkerQueueFactory workerQueueFactory,
            CursorStorageService cursorStorageService,
            CatalogScanStorageService catalogScanStorageService,
            LatestPackageLeafStorageService latestPackageLeafStorageService,
            AppendResultStorageService findPackageAssetsStorageService)
        {
            _catalogScanService = catalogScanService;
            _workerQueueFactory = workerQueueFactory;
            _cursorStorageService = cursorStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _latestPackageLeafStorageService = latestPackageLeafStorageService;
            _findPackageAssetsStorageService = findPackageAssetsStorageService;
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

            await _catalogScanService.UpdateAsync();
        }

        public bool IsInitializationRequired() => false;
        public bool IsDatabaseRequired() => false;
        public bool IsSingleton() => false;

    }
}
