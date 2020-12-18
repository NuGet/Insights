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
            serviceCollection.AddTransient<MessageBatcher>();
            serviceCollection.AddTransient<MessageEnqueuer>();

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

            foreach (var (serviceType, implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IMessageProcessor<>)))
            {
                serviceCollection.AddTransient(serviceType, implementationType);
            }

            return serviceCollection;
        }

        private static void AddFindPackageAssets(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<FindPackageAssetsScanDriver>();
        }

        private static void AddRunRealRestore(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ProjectHelper>();
        }
    }
}
