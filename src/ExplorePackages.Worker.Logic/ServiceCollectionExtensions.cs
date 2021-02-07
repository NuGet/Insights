using System.Linq;
using Knapcode.ExplorePackages.Worker.EnqueueCatalogLeafScan;
using Knapcode.ExplorePackages.Worker.FindCatalogLeafItem;
using Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScan;
using Knapcode.ExplorePackages.Worker.FindLatestPackageLeaf;
using Knapcode.ExplorePackages.Worker.FindPackageFile;
using Knapcode.ExplorePackages.Worker.FindPackageManifest;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExplorePackagesWorker(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddTransient<IWorkerQueueFactory, UnencodedWorkerQueueFactory>();

            serviceCollection.AddTransient<IGenericMessageProcessor, GenericMessageProcessor>();
            serviceCollection.AddTransient<SchemaSerializer>();
            serviceCollection.AddTransient<IMessageBatcher, MessageBatcher>();
            serviceCollection.AddTransient<IMessageEnqueuer, MessageEnqueuer>();

            serviceCollection.AddTransient(typeof(TableScanService<>));
            serviceCollection.AddTransient(typeof(TableScanDriverFactory<>));

            serviceCollection.AddTransient<CatalogScanStorageService>();
            serviceCollection.AddTransient<ICatalogScanDriverFactory, CatalogScanDriverFactory>();
            serviceCollection.AddTransient<CatalogScanService>();
            serviceCollection.AddTransient<CatalogScanExpandService>();
            serviceCollection.AddTransient(typeof(CatalogScanToCsvAdapter<>));
            AddTableScan<LatestPackageLeaf>(serviceCollection);
            AddTableScan<CatalogLeafScan>(serviceCollection);
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScan>, LatestCatalogLeafScanStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScan>>();
            serviceCollection.AddTransient<EnqueueCatalogLeafScansDriver>();

            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();
            serviceCollection.AddTransient<ICsvReader, NRecoCsvReader>();

            serviceCollection.AddFindCatalogLeafItem();
            serviceCollection.AddFindLatestLeaf();
            serviceCollection.AddFindPackageFile();
            serviceCollection.AddFindPackageManifest();
            serviceCollection.AddRunRealRestore();
            serviceCollection.AddTableCopy();

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IMessageProcessor<>)))
            {
                serviceCollection.AddTransient(serviceType, implementationType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IBatchMessageProcessor<>)))
            {
                serviceCollection.AddTransient(serviceType, implementationType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IStreamWriterUpdater<>)))
            {
                serviceCollection.AddTransient(serviceType, implementationType);

                var dataType = serviceType.GenericTypeArguments.Single();
                var messageType = typeof(StreamWriterUpdaterMessage<>).MakeGenericType(dataType);

                // Add the service
                serviceCollection.AddTransient(
                    typeof(IStreamWriterUpdaterService<>).MakeGenericType(dataType),
                    typeof(StreamWriterUpdaterService<>).MakeGenericType(dataType));

                // Add the message processor
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

                // Add the task state message processor
                serviceCollection.AddTransient(
                    typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                    typeof(LoopingMessageProcessor<>).MakeGenericType(messageType));

                // Add the looping message processor
                serviceCollection.AddTransient(
                    typeof(ILoopingMessageProcessor<>).MakeGenericType(messageType),
                    typeof(StreamWriterUpdaterProcessor<>).MakeGenericType(dataType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ITaskStateMessageProcessor<>)))
            {
                // Add the task state message processor
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the message processor
                var messageType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ILoopingMessageProcessor<>)))
            {
                // Add the task state message processor
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the message processor
                var messageType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

                // Add the task state message processor
                serviceCollection.AddTransient(
                    typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                    typeof(LoopingMessageProcessor<>).MakeGenericType(messageType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICsvCompactor<>)))
            {
                // Add the compactor
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the compact processor
                var recordType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(typeof(CsvCompactMessage<>).MakeGenericType(recordType)),
                    typeof(CsvCompactorProcessor<>).MakeGenericType(recordType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<>)))
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

        private static void AddTableCopy(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient(typeof(TableCopyDriver<>));
        }

        private static void AddTableScan<T>(IServiceCollection serviceCollection) where T : ITableEntity, new()
        {
            var entityType = typeof(T);
            serviceCollection.AddTransient(
                typeof(IMessageProcessor<>).MakeGenericType(typeof(TableScanMessage<>).MakeGenericType(entityType)),
                typeof(TableScanMessageProcessor<>).MakeGenericType(entityType));
            serviceCollection.AddTransient(
                typeof(IMessageProcessor<>).MakeGenericType(typeof(TableRowCopyMessage<>).MakeGenericType(entityType)),
                typeof(TableRowCopyMessageProcessor<>).MakeGenericType(entityType));
        }

        private static void AddFindCatalogLeafItem(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<FindCatalogLeafItemDriver>();
        }

        private static void AddFindLatestLeaf(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LatestPackageLeafStorageFactory>();
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<LatestPackageLeaf>, LatestPackageLeafStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<LatestPackageLeaf>>();
        }

        private static void AddFindPackageFile(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<FindPackageFileDriver>();
        }

        private static void AddFindPackageManifest(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<FindPackageManifestDriver>();
        }

        private static void AddRunRealRestore(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ProjectHelper>();
        }
    }
}
