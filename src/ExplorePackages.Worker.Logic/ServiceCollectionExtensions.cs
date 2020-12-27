using System.Linq;
using Knapcode.ExplorePackages.Worker.FindCatalogLeafItems;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
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
            serviceCollection.AddTransient<IMessageBatcher, MessageBatcher>();
            serviceCollection.AddTransient<MessageEnqueuer>();

            serviceCollection.AddTransient<CatalogScanStorageService>();
            serviceCollection.AddTransient<CatalogScanDriverFactory>();
            serviceCollection.AddTransient<CatalogScanService>();

            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();
            serviceCollection.AddTransient<ICsvReader, NRecoCsvReader>();

            serviceCollection.AddFindCatalogLeafItems();
            serviceCollection.AddFindLatestLeaves();
            serviceCollection.AddRunRealRestore();

            serviceCollection.AddTransient(typeof(CatalogScanToCsvAdapter<>));

            foreach (var (serviceType, implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IMessageProcessor<>)))
            {
                serviceCollection.AddTransient(serviceType, implementationType);
            }

            foreach (var (serviceType, implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICsvCompactor<>)))
            {
                // Add the compactor
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the compact processor
                var recordType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(typeof(CsvCompactMessage<>).MakeGenericType(recordType)),
                    typeof(CatalogLeafToCsvCompactProcessor<>).MakeGenericType(recordType));
            }

            foreach (var (serviceType, implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                var recordType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddTransient(
                    typeof(CatalogLeafScanToCsvAdapter<>).MakeGenericType(recordType));
            }

            return serviceCollection;
        }

        private static void AddFindCatalogLeafItems(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<FindCatalogLeafItemsDriver>();
        }

        private static void AddFindLatestLeaves(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LatestPackageLeafStorageService>();
            serviceCollection.AddTransient<FindLatestLeavesDriver>();
        }

        private static void AddRunRealRestore(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ProjectHelper>();
        }
    }
}
