// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.FindLatestCatalogLeafScan;
using NuGet.Insights.Worker.FindLatestCatalogLeafScanPerId;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.LoadPackageArchive;
using NuGet.Insights.Worker.LoadPackageManifest;
using NuGet.Insights.Worker.LoadPackageReadme;
using NuGet.Insights.Worker.LoadPackageVersion;
using NuGet.Insights.Worker.LoadSymbolPackageArchive;
#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
#endif
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.TableCopy;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public static class ServiceCollectionExtensions
    {
        private class Marker
        {
        }

        public static KustoConnectionStringBuilder GetKustoConnectionStringBuilder(NuGetInsightsWorkerSettings settings)
        {
            if (string.IsNullOrEmpty(settings.KustoConnectionString))
            {
                return new KustoConnectionStringBuilder("https://localhost:8080");
            }

            var builder = new KustoConnectionStringBuilder(settings.KustoConnectionString);

            if (settings.UserManagedIdentityClientId != null && settings.KustoUseUserManagedIdentity)
            {
                builder = builder.WithAadUserManagedIdentity(settings.UserManagedIdentityClientId);
            }

            if (settings.KustoClientCertificateContent != null)
            {
                var certificate = new X509Certificate2(settings.KustoClientCertificateContent);
                builder = builder.WithAadApplicationCertificateAuthentication(
                    builder.ApplicationClientId,
                    certificate,
                    builder.Authority,
                    builder.ApplicationCertificateSendX5c);
            }

            return builder;
        }

        private static readonly object TraceListenersLock = new object();

        public static IServiceCollection AddNuGetInsightsWorker(this IServiceCollection serviceCollection)
        {
            // Avoid re-adding all the services.
            if (serviceCollection.Any(x => x.ServiceType == typeof(Marker)))
            {
                return serviceCollection;
            }

            serviceCollection.AddSingleton<Marker>();

            serviceCollection.AddTransient<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddTransient<IWorkerQueueFactory, WorkerQueueFactory>();

            serviceCollection.AddTransient<MetricsTimer>();

            serviceCollection.AddTransient<IGenericMessageProcessor, GenericMessageProcessor>();
            serviceCollection.AddSingleton(x => SchemaCollectionBuilder.Default.Build());
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
            serviceCollection.AddTransient<CatalogScanUpdateTimer>();
            serviceCollection.AddTransient<ICatalogScanDriverFactory, CatalogScanDriverFactory>();
            serviceCollection.AddTransient<CatalogScanService>();
            serviceCollection.AddTransient<CatalogScanExpandService>();
            serviceCollection.AddTransient<CsvTemporaryStorageFactory>();
            AddTableScan<LatestPackageLeaf>(serviceCollection);
            AddTableScan<CatalogLeafScan>(serviceCollection);

            serviceCollection.AddTransient<KustoIngestionService>();
            serviceCollection.AddTransient<KustoIngestionStorageService>();
            serviceCollection.AddTransient<KustoDataValidator>();
            serviceCollection.AddTransient<KustoIngestionTimer>();
            serviceCollection.AddTransient<CsvResultStorageContainers>();
            serviceCollection.AddTransient(x =>
            {
                var options = x.GetRequiredService<IOptions<NuGetInsightsWorkerSettings>>();

                if (options.Value.EnableDiagnosticTracingToLogger)
                {
                    lock (TraceListenersLock)
                    {
                        var anyListener = Trace
                            .Listeners
                            .OfType<LoggerTraceListener>()
                            .Any();

                        if (!anyListener)
                        {
                            ContextAwareTraceFormatter.GetStaticContext(
                                out _,
                                out var machineName,
                                out var instanceId,
                                out var instanceNumericId);
                            ContextAwareTraceFormatter.SetStaticContext(
                                "KustoClientSideTracing",
                                machineName,
                                instanceId,
                                instanceNumericId);

                            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
                            Trace.Listeners.Add(new LoggerTraceListener(loggerFactory));
                        }
                    }
                }

                return GetKustoConnectionStringBuilder(options.Value);
            });
            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = x.GetRequiredService<KustoConnectionStringBuilder>();
                var adminProvider = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
                if (adminProvider is null)
                {
                    throw new InvalidOperationException($"The {nameof(ICslAdminProvider)} instance must not be null, even if there is no Kusto configuration.");
                }
                return adminProvider;
            });
            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = x.GetRequiredService<KustoConnectionStringBuilder>();
                var queryProvider = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
                if (queryProvider is null)
                {
                    throw new InvalidOperationException($"The {nameof(ICslQueryProvider)} instance must not be null, even if there is no Kusto configuration.");
                }
                return queryProvider;
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

                var ingestClient = KustoIngestFactory.CreateQueuedIngestClient(connectionStringBuilder);
                if (ingestClient is null)
                {
                    throw new InvalidOperationException($"The {nameof(IKustoQueuedIngestClient)} instance must not be null, even if there is no Kusto configuration.");
                }
                return ingestClient;
            });

            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScan>, LatestCatalogLeafScanStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScan>>();

            serviceCollection.AddTransient<ILatestPackageLeafStorageFactory<CatalogLeafScanPerId>, LatestCatalogLeafScanPerIdStorageFactory>();
            serviceCollection.AddTransient<FindLatestLeafDriver<CatalogLeafScanPerId>>();

            serviceCollection.AddTransient<EnqueueCatalogLeafScansDriver>();

            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddTransient<TimerExecutionService>();
            serviceCollection.AddTransient<SpecificTimerExecutionService>();
            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();
            serviceCollection.AddTransient<ICsvReader, CsvReaderAdapter>();

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICleanupOrphanRecordsAdapter<>)))
            {
                serviceCollection.AddCleanupOrphanRecordsService(serviceType, implementationType);
            }

            serviceCollection.AddLoadLatestPackageLeaf();
            serviceCollection.AddLoadPackageArchive();
            serviceCollection.AddLoadSymbolPackageArchive();
#if ENABLE_CRYPTOAPI
            serviceCollection.AddLoadPackageCertificate();
#endif
            serviceCollection.AddLoadPackageManifest();
            serviceCollection.AddLoadPackageReadme();
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

                serviceCollection.AddTransient(
                    typeof(IAuxiliaryFileUpdaterService),
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
                serviceCollection.AddTransient(
                    typeof(IAuxiliaryFileUpdaterTimer),
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

            AddCsvNonBatchDrivers(serviceCollection);
            AddCsvBatchDrivers(serviceCollection);

            return serviceCollection;
        }

        private static void AddCsvNonBatchDrivers(IServiceCollection serviceCollection)
        {
            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvNonBatchAdapter<>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<,>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvNonBatchAdapter<,>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvDriver<,,>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvNonBatchAdapter<,,>).MakeGenericType(serviceType.GenericTypeArguments));
            }
        }

        private static void AddCsvBatchDrivers(IServiceCollection serviceCollection)
        {
            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvBatchDriver<>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvBatchAdapter<>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvBatchDriver<,>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvBatchAdapter<,>).MakeGenericType(serviceType.GenericTypeArguments));
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICatalogLeafToCsvBatchDriver<,,>)))
            {
                // Add the driver
                serviceCollection.AddTransient(serviceType, implementationType);

                // Add the catalog scan adapter
                serviceCollection.AddTransient(typeof(CatalogLeafScanToCsvBatchAdapter<,,>).MakeGenericType(serviceType.GenericTypeArguments));
            }
        }

        public static void AddCleanupOrphanRecordsService<TService, TRecord>(this IServiceCollection serviceCollection)
            where TService : class, ICleanupOrphanRecordsAdapter<TRecord>
            where TRecord : ICsvRecord
        {
            var implementationType = typeof(TService);
            var dataType = typeof(TRecord);
            var serviceType = typeof(ICleanupOrphanRecordsAdapter<>).MakeGenericType(dataType);
            serviceCollection.AddCleanupOrphanRecordsService(serviceType, implementationType);
        }

        private static void AddCleanupOrphanRecordsService(this IServiceCollection serviceCollection, Type serviceType, Type implementationType)
        {
            var dataType = serviceType.GenericTypeArguments.Single();
            var messageType = typeof(CleanupOrphanRecordsMessage<>).MakeGenericType(dataType);

            // Add the adapter
            serviceCollection.AddTransient(serviceType, implementationType);

            // Add the service
            serviceCollection.AddTransient(
                typeof(ICleanupOrphanRecordsService<>).MakeGenericType(dataType),
                typeof(CleanupOrphanRecordsService<>).MakeGenericType(dataType));
            serviceCollection.AddTransient(
                typeof(ICleanupOrphanRecordsService),
                typeof(CleanupOrphanRecordsService<>).MakeGenericType(dataType));

            // Add the message processor
            serviceCollection.AddTransient(
                typeof(IMessageProcessor<>).MakeGenericType(messageType),
                typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

            // Add the task state message processor
            serviceCollection.AddTransient(
                typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                typeof(CleanupOrphanRecordsProcessor<>).MakeGenericType(dataType));

            // Add the timer
            serviceCollection.AddTransient(
                typeof(ITimer),
                typeof(CleanupOrphanRecordsTimer<>).MakeGenericType(dataType));
            serviceCollection.AddTransient(
                typeof(ICleanupOrphanRecordsTimer),
                typeof(CleanupOrphanRecordsTimer<>).MakeGenericType(dataType));
        }

        private static void AddTableCopy(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient(typeof(TableCopyDriver<>));
        }

        private static void AddTableScan<T>(IServiceCollection serviceCollection) where T : ITableEntityWithClientRequestId, new()
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
            serviceCollection.AddSingleton<VersionSetService>();
            serviceCollection.AddSingleton<IVersionSetProvider>(s => s.GetRequiredService<VersionSetService>());
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

        private static void AddLoadSymbolPackageArchive(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadSymbolPackageArchiveDriver>();
        }

#if ENABLE_CRYPTOAPI
        private static void AddLoadPackageCertificate(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<PackageCertificateToCsvDriver>();
        }
#endif

        private static void AddLoadPackageManifest(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadPackageManifestDriver>();
        }

        private static void AddLoadPackageReadme(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadPackageReadmeDriver>();
        }

        private static void AddLoadPackageVersion(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddTransient<LoadPackageVersionDriver>();
            serviceCollection.AddTransient<PackageVersionStorageService>();
        }
    }
}
