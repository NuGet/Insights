// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Azure.Data.Tables;
using Kusto.Data;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;
using NuGet.Insights.Worker.CatalogLeafItemToCsv;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.FindLatestCatalogLeafScan;
using NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.LoadPackageArchive;
using NuGet.Insights.Worker.LoadPackageManifest;
using NuGet.Insights.Worker.LoadPackageVersion;
using NuGet.Insights.Worker.TableCopy;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddNuGetInsightsWorker(this IServiceCollection serviceCollection)
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

            serviceCollection.AddTransient<WorkflowStorageService>();
            serviceCollection.AddTransient<WorkflowService>();

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
            serviceCollection.AddTransient<CsvResultStorageContainers>();
            serviceCollection.AddTransient(x =>
            {
                var options = x.GetRequiredService<IOptions<NuGetInsightsWorkerSettings>>();
                var builder = new KustoConnectionStringBuilder(options.Value.KustoConnectionString);
                if (options.Value.UserManagedIdentityClientId != null && options.Value.KustoUseUserManagedIdentity)
                {
                    builder = builder.WithAadUserManagedIdentity(options.Value.UserManagedIdentityClientId);
                }

                return builder;
            });
            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = x.GetRequiredService<KustoConnectionStringBuilder>();
                return KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
            });
            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = x.GetRequiredService<KustoConnectionStringBuilder>();

                const string prefix = "https://";
                if (connectionStringBuilder.DataSource == null || !connectionStringBuilder.DataSource.StartsWith(prefix))
                {
                    throw new InvalidOperationException($"The Kusto connection must have a data source that starts with '{prefix}'.");
                }
                connectionStringBuilder.DataSource = prefix + "ingest-" + connectionStringBuilder.DataSource.Substring(prefix.Length);

                return KustoIngestFactory.CreateQueuedIngestClient(connectionStringBuilder);
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
            serviceCollection.AddTransient<ICsvReader, CsvReaderAdapter>();

            serviceCollection.AddCatalogLeafItemToCsv();
            serviceCollection.AddLoadLatestPackageLeaf();
            serviceCollection.AddLoadPackageArchive();
            serviceCollection.AddLoadPackageManifest();
            serviceCollection.AddLoadPackageVersion();
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

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IAuxiliaryFileUpdater<>)))
            {
                serviceCollection.AddTransient(serviceType, implementationType);

                var dataType = serviceType.GenericTypeArguments.Single();
                var messageType = typeof(AuxiliaryFileUpdaterMessage<>).MakeGenericType(dataType);

                // Add the service
                serviceCollection.AddTransient(
                     typeof(IAuxiliaryFileUpdaterService<>).MakeGenericType(dataType),
                    typeof(AuxiliaryFileUpdaterService<>).MakeGenericType(dataType));

                // Add the generic CSV storage
                var getContainerName = serviceType.GetProperty(nameof(IAuxiliaryFileUpdater<IAsOfData>.ContainerName));
                var getRecordType = serviceType.GetProperty(nameof(IAuxiliaryFileUpdater<IAsOfData>.RecordType));
                var getBlobName = serviceType.GetProperty(nameof(IAuxiliaryFileUpdater<IAsOfData>.BlobName));
                serviceCollection.AddTransient<ICsvResultStorage>(x =>
                {
                    var updater = x.GetRequiredService(serviceType);
                    var blobName = AuxiliaryFileUpdaterProcessor<IAsOfData>.GetLatestBlobName((string)getBlobName.GetValue(updater));
                    return new CsvResultStorage(
                        (string)getContainerName.GetValue(updater),
                        (Type)getRecordType.GetValue(updater),
                        blobName);
                });

                // Add the message processor
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

                // Add the task state message processor
                serviceCollection.AddTransient(
                    typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                    typeof(AuxiliaryFileUpdaterProcessor<>).MakeGenericType(dataType));

                // Add the timer
                serviceCollection.AddTransient(
                    typeof(ITimer),
                    typeof(AuxiliaryFileUpdaterTimer<>).MakeGenericType(dataType));
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

                // Add the generic CSV storage
                var recordType = serviceType.GenericTypeArguments.Single();
                var getContainerName = serviceType.GetProperty(nameof(ICsvResultStorage<ICsvRecord>.ResultContainerName));
                serviceCollection.AddTransient<ICsvResultStorage>(x =>
                {
                    var storage = x.GetRequiredService(serviceType);
                    return new CsvResultStorage(
                        (string)getContainerName.GetValue(storage),
                        recordType,
                        AppendResultStorageService.CompactPrefix);
                });

                // Add the CSV compactor processor
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
    }
}
