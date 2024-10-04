// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Kusto.Cloud.Platform.Utils;
using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
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

            serviceCollection.AddSingleton<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddSingleton<IWorkerQueueFactory, WorkerQueueFactory>();

            serviceCollection.AddSingleton<MetricsTimer>();

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

            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = GetKustoConnectionStringBuilder(x, addIngest: false);

                var adminProvider = KustoClientFactory.CreateCslAdminProvider(connectionStringBuilder);
                if (adminProvider is null)
                {
                    throw new InvalidOperationException($"The {nameof(ICslAdminProvider)} instance must not be null, even if there is no Kusto configuration.");
                }

                return adminProvider;
            });
            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = GetKustoConnectionStringBuilder(x, addIngest: false);

                var queryProvider = KustoClientFactory.CreateCslQueryProvider(connectionStringBuilder);
                if (queryProvider is null)
                {
                    throw new InvalidOperationException($"The {nameof(ICslQueryProvider)} instance must not be null, even if there is no Kusto configuration.");
                }

                return queryProvider;
            });
            serviceCollection.AddSingleton(x =>
            {
                var connectionStringBuilder = GetKustoConnectionStringBuilder(x, addIngest: true);

                var ingestClient = KustoIngestFactory.CreateQueuedIngestClient(connectionStringBuilder);
                if (ingestClient is null)
                {
                    throw new InvalidOperationException($"The {nameof(IKustoQueuedIngestClient)} instance must not be null, even if there is no Kusto configuration.");
                }

                return ingestClient;
            });

            serviceCollection.AddSingleton<CursorStorageService>();

            serviceCollection.AddSingleton<IComparer<ITimer>>(TimerComparer.Instance);
            serviceCollection.AddSingleton<TimerExecutionService>();
            serviceCollection.AddSingleton<SpecificTimerExecutionService>();
            serviceCollection.AddSingleton<AppendResultStorageService>();
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

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(IAuxiliaryFileUpdater<>)))
            {
                serviceCollection.AddSingleton(serviceType, implementationType);
                serviceCollection.AddSingleton(typeof(IAuxiliaryFileUpdater), implementationType);

                var dataType = serviceType.GenericTypeArguments.Single();
                var messageType = typeof(AuxiliaryFileUpdaterMessage<>).MakeGenericType(dataType);

                // Add the service
                serviceCollection.AddSingleton(
                    typeof(IAuxiliaryFileUpdaterService<>).MakeGenericType(dataType),
                    typeof(AuxiliaryFileUpdaterService<>).MakeGenericType(dataType));

                serviceCollection.AddSingleton(
                    typeof(IAuxiliaryFileUpdaterService),
                    typeof(AuxiliaryFileUpdaterService<>).MakeGenericType(dataType));

                // Add the generic CSV storage
                var getContainerName = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.ContainerName));
                var getRecordType = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.RecordType));
                var getBlobName = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.BlobName));
                serviceCollection.AddSingleton<ICsvRecordStorage>(x =>
                {
                    var updater = x.GetRequiredService(serviceType);
                    var blobName = AuxiliaryFileUpdaterProcessor<IAsOfData>.GetLatestBlobName((string)getBlobName.GetValue(updater));
                    return new CsvRecordStorage(
                        (string)getContainerName.GetValue(updater),
                        (Type)getRecordType.GetValue(updater),
                        blobName);
                });

                // Add the message processor
                serviceCollection.AddSingleton(
                    typeof(IMessageProcessor<>).MakeGenericType(messageType),
                    typeof(TaskStateMessageProcessor<>).MakeGenericType(messageType));

                // Add the task state message processor
                serviceCollection.AddSingleton(
                    typeof(ITaskStateMessageProcessor<>).MakeGenericType(messageType),
                    typeof(AuxiliaryFileUpdaterProcessor<>).MakeGenericType(dataType));

                // Add the timer
                serviceCollection.AddSingleton(
                    typeof(ITimer),
                    typeof(AuxiliaryFileUpdaterTimer<>).MakeGenericType(dataType));
                serviceCollection.AddSingleton(
                    typeof(IAuxiliaryFileUpdaterTimer),
                    typeof(AuxiliaryFileUpdaterTimer<>).MakeGenericType(dataType));
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
                serviceCollection.AddSingleton<ICsvRecordStorage>(x =>
                {
                    var storage = x.GetRequiredService(serviceType);
                    return new CsvRecordStorage(
                        (string)getContainerName.GetValue(storage),
                        recordType,
                        AppendResultStorageService.CompactPrefix);
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

        private static KustoConnectionStringBuilder GetKustoConnectionStringBuilder(IServiceProvider provider, bool addIngest)
        {
            var settings = provider.GetRequiredService<IOptions<NuGetInsightsWorkerSettings>>().Value;

            if (settings.EnableDiagnosticTracingToLogger)
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

                        var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                        Trace.Listeners.Add(new LoggerTraceListener(loggerFactory));
                    }
                }
            }

            return GetKustoConnectionStringBuilder(settings, addIngest);
        }

        public static KustoConnectionStringBuilder GetKustoConnectionStringBuilder(NuGetInsightsWorkerSettings settings, bool addIngest)
        {
            if (string.IsNullOrEmpty(settings.KustoConnectionString))
            {
                return new KustoConnectionStringBuilder("https://localhost:8080");
            }

            var builder = new KustoConnectionStringBuilder(settings.KustoConnectionString);

            if (settings.KustoClientCertificatePath != null)
            {
                var certificate = new X509Certificate2(settings.KustoClientCertificatePath);
                builder = builder.WithAadApplicationCertificateAuthentication(
                    builder.ApplicationClientId,
                    certificate,
                    builder.Authority,
                    builder.ApplicationCertificateSendX5c);
            }
            else if (settings.KustoClientCertificateKeyVault != null)
            {
                var certificate = CredentialCache.GetLazyCertificate(
                    settings.KustoClientCertificateKeyVault,
                    settings.KustoClientCertificateKeyVaultCertificateName).Value;
                builder = builder.WithAadApplicationCertificateAuthentication(
                    builder.ApplicationClientId,
                    certificate,
                    builder.Authority,
                    builder.ApplicationCertificateSendX5c);
            }
            else if (settings.UserManagedIdentityClientId != null && settings.KustoUseUserManagedIdentity)
            {
                builder = builder.WithAadUserManagedIdentity(settings.UserManagedIdentityClientId);
            }
            else
            {
                builder = builder.WithAadAzureTokenCredentialsAuthentication(CredentialCache.DefaultAzureCredential);
            }

            const string prefix = "https://";
            if (builder.DataSource == null || !builder.DataSource.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The Kusto connection must have a data source that starts with '{prefix}'.");
            }

            if (addIngest)
            {
                builder.DataSource = prefix + "ingest-" + builder.DataSource.Substring(prefix.Length);
            }

            return builder;
        }

        private static readonly object TraceListenersLock = new object();

    }
}
