using System.Linq;
using Azure.Data.Tables;
using Knapcode.ExplorePackages.Worker.BuildVersionSet;
using Knapcode.ExplorePackages.Worker.CatalogLeafItemToCsv;
using Knapcode.ExplorePackages.Worker.EnqueueCatalogLeafScan;
using Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScan;
using Knapcode.ExplorePackages.Worker.FindLatestCatalogLeafScanPerId;
using Knapcode.ExplorePackages.Worker.KustoIngestion;
using Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf;
using Knapcode.ExplorePackages.Worker.LoadPackageArchive;
using Knapcode.ExplorePackages.Worker.LoadPackageManifest;
using Knapcode.ExplorePackages.Worker.LoadPackageVersion;
using Knapcode.ExplorePackages.Worker.RunRealRestore;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Kusto.Data;
using Kusto.Data.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExplorePackagesWorker(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddTransient<IWorkerQueueFactory, WorkerQueueFactory>();

            serviceCollection.AddTransient<IGenericMessageProcessor, GenericMessageProcessor>();
            serviceCollection.AddTransient<SchemaSerializer>();
            serviceCollection.AddTransient<IMessageBatcher, MessageBatcher>();
            serviceCollection.AddTransient<IMessageEnqueuer, MessageEnqueuer>();

            serviceCollection.AddTransient(typeof(TableScanService<>));
            serviceCollection.AddTransient(typeof(TableScanDriverFactory<>));
            serviceCollection.AddTransient(typeof(LatestLeafStorageService<>));

            serviceCollection.AddTransient<CatalogScanStorageService>();
            serviceCollection.AddTransient<CatalogScanCursorService>();
            serviceCollection.AddTransient<ICatalogScanDriverFactory, CatalogScanDriverFactory>();
            serviceCollection.AddTransient<CatalogScanService>();
            serviceCollection.AddTransient<CatalogScanExpandService>();
            serviceCollection.AddTransient<CsvTemporaryStorageFactory>();
            AddTableScan<LatestPackageLeaf>(serviceCollection);
            AddTableScan<CatalogLeafScan>(serviceCollection);

            serviceCollection.AddTransient<KustoIngestionService>();
            serviceCollection.AddTransient<KustoIngestionStorageService>();
            serviceCollection.AddTransient<CsvRecordContainers>();
            serviceCollection.AddSingleton(x =>
            {
                var options = x.GetRequiredService<IOptions<ExplorePackagesWorkerSettings>>();
                var connectionStringBuilder = new KustoConnectionStringBuilder(options.Value.KustoConnectionString);
                return KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
            });

            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScan>, LatestCatalogLeafScanStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScan>>();

            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScanPerId>, LatestCatalogLeafScanPerIdStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScanPerId>>();

            serviceCollection.AddTransient<EnqueueCatalogLeafScansDriver>();

            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddTransient<TimerExecutionService>();
            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();
            serviceCollection.AddTransient<ICsvReader, NRecoCsvReader>();

            serviceCollection.AddCatalogLeafItemToCsv();
            serviceCollection.AddLoadLatestPackageLeaf();
            serviceCollection.AddLoadPackageArchive();
            serviceCollection.AddLoadPackageManifest();
            serviceCollection.AddLoadPackageVersion();
            serviceCollection.AddRunRealRestore();
            serviceCollection.AddTableCopy();
            serviceCollection.AddBuildVersionSet();

            foreach (var serviceType in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementing<ITimer>())
            {
                serviceCollection.AddTransient(typeof(ITimer), serviceType);
            }

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
                    typeof(StreamWriterUpdaterProcessor<>).MakeGenericType(dataType));

                // Add the timer
                serviceCollection.AddTransient(
                    typeof(ITimer),
                    typeof(StreamWriterUpdaterTimer<>).MakeGenericType(dataType));
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

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICsvResultStorage<>)))
            {
                // Add the CSV storage
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the CSV compactor processor
                var recordType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(typeof(CsvCompactMessage<>).MakeGenericType(recordType)),
                    typeof(CsvCompactorProcessor<>).MakeGenericType(recordType));

                // Add custom expand processor
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(typeof(CsvExpandReprocessMessage<>).MakeGenericType(recordType)),
                    typeof(CsvExpandReprocessProcessor<>).MakeGenericType(recordType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvAdapter<>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<,>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvAdapter<,>).MakeGenericType(serviceType.GenericTypeArguments));
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

        private static void AddBuildVersionSet(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<BuildVersionSetDriver>();
            serviceCollection.AddTransient<VersionSetAggregateStorageService>();
            serviceCollection.AddTransient<VersionSetService>();
            serviceCollection.AddTransient<IVersionSetProvider, VersionSetService>();
        }

        private static void AddCatalogLeafItemToCsv(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<CatalogLeafItemToCsvDriver>();
        }

        private static void AddLoadLatestPackageLeaf(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LatestPackageLeafService>();
            serviceCollection.AddTransient<LatestPackageLeafStorageFactory>();
            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<LatestPackageLeaf>, LatestPackageLeafStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<LatestPackageLeaf>>();
        }

        private static void AddLoadPackageArchive(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadPackageArchiveDriver>();
        }

        private static void AddLoadPackageManifest(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadPackageManifestDriver>();
        }

        private static void AddLoadPackageVersion(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadPackageVersionDriver>();
            serviceCollection.AddTransient<PackageVersionStorageService>();
        }

        private static void AddRunRealRestore(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<ProjectHelper>();
        }
    }
}
