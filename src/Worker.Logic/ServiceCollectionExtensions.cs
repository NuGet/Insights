// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Kusto;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.TableCopy;
using NuGet.Insights.Worker.TimedReprocess;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public static partial class ServiceCollectionExtensions
    {
        private class Marker
        {
        }

        private static IReadOnlyList<Action<IServiceCollection>> AdditionalSetupActions = typeof(ServiceCollectionExtensions)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(x => x.Name.StartsWith("Setup", StringComparison.Ordinal)
                        && x.GetParameters().Length == 1
                        && x.GetParameters()[0].ParameterType == typeof(IServiceCollection))
            .Select(x => (Action<IServiceCollection>)(y => x.Invoke(null, [y])))
            .ToList();

        public static IServiceCollection AddNuGetInsightsWorker(this IServiceCollection serviceCollection)
        {
            // Avoid re-adding all the services.
            if (serviceCollection.Any(x => x.ServiceType == typeof(Marker)))
            {
                return serviceCollection;
            }

            serviceCollection.AddSingleton<Marker>();

            foreach (var setup in AdditionalSetupActions)
            {
                setup(serviceCollection);
            }

            serviceCollection.AddSingleton<CachingKustoClientFactory>();

            serviceCollection.AddSingleton<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddSingleton<IWorkerQueueFactory, WorkerQueueFactory>();

            serviceCollection.AddSingleton<MetricsTimer>();

            serviceCollection.AddSingleton<PackageFilter>();

            serviceCollection.AddSingleton<IGenericMessageProcessor, GenericMessageProcessor>();
            serviceCollection.AddSingleton(x => SchemaCollectionBuilder.Default.Build());
            serviceCollection.AddSingleton<SchemaSerializer>();
            serviceCollection.AddSingleton<IMessageBatcher, MessageBatcher>();
            serviceCollection.AddSingleton<IMessageEnqueuer, MessageEnqueuer>();

            serviceCollection.AddSingleton<FanOutRecoveryService>();

            serviceCollection.AddSingleton<TableScanService>();
            serviceCollection.AddSingleton(typeof(TableScanDriverFactory<>));
            serviceCollection.AddSingleton(typeof(LatestLeafStorageService<>));

            serviceCollection.AddSingleton<WorkflowStorageService>();
            serviceCollection.AddSingleton<WorkflowService>();

            serviceCollection.AddSingleton<CatalogScanStorageService>();
            serviceCollection.AddSingleton<CatalogScanCursorService>();
            serviceCollection.AddSingleton<CatalogScanUpdateTimer>();
            serviceCollection.AddSingleton<ICatalogScanDriverFactory, CatalogScanDriverFactory>();
            serviceCollection.AddSingleton<CatalogScanService>();
            serviceCollection.AddSingleton<CatalogScanExpandService>();
            serviceCollection.AddSingleton<CsvTemporaryStorageFactory>();
            AddTableScan<CatalogLeafScan>(serviceCollection);

            serviceCollection.AddSingleton<KustoIngestionService>();
            serviceCollection.AddSingleton<KustoIngestionStorageService>();
            serviceCollection.AddSingleton<KustoDataValidator>();
            serviceCollection.AddSingleton<KustoIngestionTimer>();

            foreach (var serviceType in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementing<IKustoValidationProvider>())
            {
                serviceCollection.AddSingleton(typeof(IKustoValidationProvider), serviceType);
            }

            serviceCollection.AddSingleton<CursorStorageService>();

            serviceCollection.AddSingleton<IComparer<ITimer>>(TimerComparer.Instance);
            serviceCollection.AddSingleton<TimerExecutionService>();
            serviceCollection.AddSingleton<SpecificTimerExecutionService>();
            serviceCollection.AddSingleton<AppendResultStorageService>();
            serviceCollection.AddSingleton<CsvRecordStorageService>();
            serviceCollection.AddSingleton<TaskStateStorageService>();
            serviceCollection.AddSingleton<ICsvReader, CsvReaderAdapter>();
            serviceCollection.AddSingleton<CsvRecordContainers>();

            serviceCollection.AddSingleton(typeof(TableCopyDriver<>));

            serviceCollection.AddSingleton<TimedReprocessService>();
            serviceCollection.AddSingleton<TimedReprocessStorageService>();
            serviceCollection.AddSingleton<TimedReprocessTimer>();

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ITableScanDriver<>)))
            {
                serviceCollection.AddSingleton(implementationType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICleanupOrphanRecordsAdapter<>)))
            {
                AddCleanupOrphanRecordsService(serviceCollection, serviceType, implementationType);
            }

            foreach (var serviceType in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementing<ITimer>())
            {
                serviceCollection.AddSingleton(typeof(ITimer), serviceType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IMessageProcessor<>)))
            {
                serviceCollection.AddSingleton(serviceType, implementationType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IBatchMessageProcessor<>)))
            {
                serviceCollection.AddSingleton(serviceType, implementationType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IAuxiliaryFileUpdater<,>)))
            {
                serviceCollection.AddSingleton(serviceType, implementationType);
                serviceCollection.AddSingleton(typeof(IAuxiliaryFileUpdater), implementationType);

                var inputType = serviceType.GenericTypeArguments[0];
                var recordType = serviceType.GenericTypeArguments[1];
                var messageType = typeof(AuxiliaryFileUpdaterMessage<>).MakeGenericType(recordType);

                // Add the service
                serviceCollection.AddSingleton(
                    typeof(IAuxiliaryFileUpdaterService<>).MakeGenericType(recordType),
                    typeof(AuxiliaryFileUpdaterService<,>).MakeGenericType(inputType, recordType));

                serviceCollection.AddSingleton(x => (IAuxiliaryFileUpdaterService)x.GetRequiredService(typeof(IAuxiliaryFileUpdaterService<>).MakeGenericType(recordType)));

                // Add the generic CSV storage
                var getContainerName = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.ContainerName));
                serviceCollection.AddSingleton(x =>
                {
                    var updater = x.GetRequiredService(serviceType);
                    return new CsvRecordContainerInfo(
                        (string)getContainerName.GetValue(updater),
                        recordType,
                        CsvRecordStorageService.CompactPrefix);
                });

                // Add the message processor
                serviceCollection.AddSingleton(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

                // Add the task state message processor
                serviceCollection.AddSingleton(
                    typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                    typeof(AuxiliaryFileUpdaterProcessor<,>).MakeGenericType(inputType, recordType));

                // Add the timer
                serviceCollection.AddSingleton(
                    typeof(ITimer),
                    typeof(AuxiliaryFileUpdaterTimer<,>).MakeGenericType(inputType, recordType));
                serviceCollection.AddSingleton(
                    typeof(IAuxiliaryFileUpdaterTimer),
                    typeof(AuxiliaryFileUpdaterTimer<,>).MakeGenericType(inputType, recordType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ITaskStateMessageProcessor<>)))
            {
                // Add the task state message processor
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the message processor
                var messageType = serviceType.GenericTypeArguments.Single();
                serviceCollection.AddSingleton(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICsvResultStorage<>)))
            {
                // Add the CSV storage
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the generic CSV storage
                var recordType = serviceType.GenericTypeArguments.Single();
                var getContainerName = serviceType.GetProperty("ResultContainerName");
                serviceCollection.AddSingleton(x =>
                {
                    var storage = x.GetRequiredService(serviceType);
                    return new CsvRecordContainerInfo(
                        (string)getContainerName.GetValue(storage),
                        recordType,
                        CsvRecordStorageService.CompactPrefix);
                });

                // Add the CSV compactor processor
                var messageType = typeof(CsvCompactMessage<>).MakeGenericType(recordType);
                serviceCollection.AddSingleton(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));
                serviceCollection.AddSingleton(
                    typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                    typeof(CsvCompactProcessor<>).MakeGenericType(recordType));
            }

            AddCsvNonBatchDrivers(serviceCollection);
            AddCsvBatchDrivers(serviceCollection);

            return serviceCollection;
        }

        private static void AddCsvNonBatchDrivers(IServiceCollection serviceCollection)
        {
            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<>)))
            {
                // Add the driver
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddSingleton(typeof(CatalogLeafScanToCsvNonBatchAdapter<>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<,>)))
            {
                // Add the driver
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddSingleton(typeof(CatalogLeafScanToCsvNonBatchAdapter<,>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<,,>)))
            {
                // Add the driver
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddSingleton(typeof(CatalogLeafScanToCsvNonBatchAdapter<,,>).MakeGenericType(serviceType.GenericTypeArguments));
            }
        }

        private static void AddCsvBatchDrivers(IServiceCollection serviceCollection)
        {
            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvBatchDriver<>)))
            {
                // Add the driver
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddSingleton(typeof(CatalogLeafScanToCsvBatchAdapter<>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvBatchDriver<,>)))
            {
                // Add the driver
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddSingleton(typeof(CatalogLeafScanToCsvBatchAdapter<,>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvBatchDriver<,,>)))
            {
                // Add the driver
                serviceCollection.AddSingleton(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddSingleton(typeof(CatalogLeafScanToCsvBatchAdapter<,,>).MakeGenericType(serviceType.GenericTypeArguments));
            }
        }

        public static void AddCleanupOrphanRecordsService<TService, TRecord>(this IServiceCollection serviceCollection)
            where TService : class, ICleanupOrphanRecordsAdapter<TRecord>
            where TRecord : IAggregatedCsvRecord<TRecord>, ICleanupOrphanCsvRecord
        {
            var implementationType = typeof(TService);
            var dataType = typeof(TRecord);
            var serviceType = typeof(ICleanupOrphanRecordsAdapter<>).MakeGenericType(dataType);
            AddCleanupOrphanRecordsService(serviceCollection, serviceType, implementationType);
        }

        private static void AddCleanupOrphanRecordsService(IServiceCollection serviceCollection, Type serviceType, Type implementationType)
        {
            var dataType = serviceType.GenericTypeArguments.Single();
            var messageType = typeof(CleanupOrphanRecordsMessage<>).MakeGenericType(dataType);

            // Add the adapter
            serviceCollection.AddSingleton(serviceType, implementationType);

            // Add the service
            serviceCollection.AddSingleton(
                typeof(ICleanupOrphanRecordsService<>).MakeGenericType(dataType),
                typeof(CleanupOrphanRecordsService<>).MakeGenericType(dataType));
            serviceCollection.AddSingleton(
                typeof(ICleanupOrphanRecordsService),
                typeof(CleanupOrphanRecordsService<>).MakeGenericType(dataType));

            // Add the message processor
            serviceCollection.AddSingleton(
                typeof(IMessageProcessor<>).MakeGenericType(messageType),
                typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

            // Add the task state message processor
            serviceCollection.AddSingleton(
                typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                typeof(CleanupOrphanRecordsProcessor<>).MakeGenericType(dataType));

            // Add the timer
            serviceCollection.AddSingleton(
                typeof(ITimer),
                typeof(CleanupOrphanRecordsTimer<>).MakeGenericType(dataType));
            serviceCollection.AddSingleton(
                typeof(ICleanupOrphanRecordsTimer),
                typeof(CleanupOrphanRecordsTimer<>).MakeGenericType(dataType));
        }

        private static void AddTableScan<T>(IServiceCollection serviceCollection) where T : ITableEntityWithClientRequestId, new()
        {
            var entityType = typeof(T);
            var tableScanMessageType = typeof(TableScanMessage<>).MakeGenericType(entityType);
            serviceCollection.AddSingleton(
                typeof(IMessageProcessor<>).MakeGenericType(tableScanMessageType),
                typeof(TaskStateMessageProcessor<>).MakeGenericType(tableScanMessageType));
            serviceCollection.AddSingleton(
                typeof(ITaskStateMessageProcessor<>).MakeGenericType(tableScanMessageType),
                typeof(TableScanMessageProcessor<>).MakeGenericType(entityType));
            serviceCollection.AddSingleton(
                typeof(IMessageProcessor<>).MakeGenericType(typeof(TableRowCopyMessage<>).MakeGenericType(entityType)),
                typeof(TableRowCopyMessageProcessor<>).MakeGenericType(entityType));
        }
    }
}
