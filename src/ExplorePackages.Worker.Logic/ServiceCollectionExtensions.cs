using Knapcode.ExplorePackages.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using Microsoft.Extensions.DependencyInjection;

namespace Knapcode.ExplorePackages.Worker
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExplorePackagesWorker(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddTransient<IWorkerQueueFactory, UnencodedWorkerQueueFactory>();

            serviceCollection.AddTransient<GenericMessageProcessor>();
            serviceCollection.AddTransient<SchemaSerializer>();
            serviceCollection.AddTransient<MessageEnqueuer>();

            serviceCollection.AddTransient<IMessageProcessor<BulkEnqueueMessage>, BulkEnqueueMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<CatalogIndexScanMessage>, CatalogIndexScanMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<CatalogPageScanMessage>, CatalogPageScanMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<CatalogLeafScanMessage>, CatalogLeafScanMessageProcessor>();

            serviceCollection.AddTransient<CatalogScanStorageService>();
            serviceCollection.AddTransient<LatestPackageLeafStorageService>();
            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();

            serviceCollection.AddTransient<CatalogScanDriverFactory>();
            serviceCollection.AddTransient<DownloadLeavesCatalogScanDriver>();
            serviceCollection.AddTransient<DownloadPagesCatalogScanDriver>();
            serviceCollection.AddTransient<FindLatestLeavesCatalogScanDriver>();

            serviceCollection.AddTransient<CatalogScanService>();

            serviceCollection.AddTransient<ICsvReader, NRecoCsvReader>();

            serviceCollection.AddFindPackageAssets();
            serviceCollection.AddRunRealRestore();

            return serviceCollection;
        }

        private static void AddFindPackageAssets(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMessageProcessor<FindPackageAssetsCompactMessage>, FindPackageAssetsCompactProcessor>();

            serviceCollection.AddTransient<FindPackageAssetsScanDriver>();
        }

        private static void AddRunRealRestore(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IMessageProcessor<RunRealRestoreMessage>, RunRealRestoreProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<RunRealRestoreCompactMessage>, RunRealRestoreCompactProcessor>();

            serviceCollection.AddTransient<ProjectHelper>();
        }
    }
}
