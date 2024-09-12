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

        public static KustoConnectionStringBuilder GetKustoConnectionStringBuilder(NuGetInsightsWorkerSettings settings)
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

            return builder;
        }

        private static readonly object TraceListenersLock = new object();

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

            serviceCollection.AddTransient<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddTransient<IWorkerQueueFactory, WorkerQueueFactory>();

            serviceCollection.AddTransient<MetricsTimer>();

            serviceCollection.AddTransient<IGenericMessageProcessor, GenericMessageProcessor>();
            serviceCollection.AddSingleton(x => SchemaCollectionBuilder.Default.Build());
            serviceCollection.AddTransient<SchemaSerializer>();
            serviceCollection.AddTransient<IMessageBatcher, MessageBatcher>();
            serviceCollection.AddTransient<IMessageEnqueuer, MessageEnqueuer>();

            serviceCollection.AddTransient<TableScanService>();
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
            AddTableScan<CatalogLeafScan>(serviceCollection);

            serviceCollection.AddTransient<KustoIngestionService>();
            serviceCollection.AddTransient<KustoIngestionStorageService>();
            serviceCollection.AddTransient<KustoDataValidator>();
            serviceCollection.AddTransient<KustoIngestionTimer>();

            foreach (var serviceType in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementing<IKustoValidationProvider>())
            {
                serviceCollection.AddTransient(typeof(IKustoValidationProvider), serviceType);
            }

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
                if (connectionStringBuilder.DataSource == null || !connectionStringBuilder.DataSource.StartsWith(prefix, StringComparison.Ordinal))
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

            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddSingleton<IComparer<ITimer>>(TimerComparer.Instance);
            serviceCollection.AddTransient<TimerExecutionService>();
            serviceCollection.AddTransient<SpecificTimerExecutionService>();
            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();
            serviceCollection.AddTransient<ICsvReader, CsvReaderAdapter>();
            serviceCollection.AddSingleton<CsvRecordContainers>();

            serviceCollection.AddTransient(typeof(TableCopyDriver<>));

            serviceCollection.AddTransient<TimedReprocessService>();
            serviceCollection.AddTransient<TimedReprocessStorageService>();
            serviceCollection.AddTransient<TimedReprocessTimer>();

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ITableScanDriver<>)))
            {
                serviceCollection.AddTransient(implementationType);
            }

            foreach ((var serviceType, var implementationType) in typeof(ServiceCollectionExtensions).Assembly.GetClassesImplementingGeneric(typeof(ICleanupOrphanRecordsAdapter<>)))
            {
                AddCleanupOrphanRecordsService(serviceCollection, serviceType, implementationType);
            }

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
                serviceCollection.AddTransient(typeof(IAuxiliaryFileUpdater), implementationType);

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
                var getContainerName = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.ContainerName));
                var getRecordType = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.RecordType));
                var getBlobName = typeof(IAuxiliaryFileUpdater).GetProperty(nameof(IAuxiliaryFileUpdater.BlobName));
                serviceCollection.AddTransient<ICsvRecordStorage>(x =>
                {
                    var updater = x.GetRequiredService(serviceType);
                    var blobName = AuxiliaryFileUpdaterProcessor<IAsOfData>.GetLatestBlobName((string)getBlobName.GetValue(updater));
                    return new CsvRecordStorage(
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
                var getContainerName = serviceType.GetProperty("ResultContainerName");
                serviceCollection.AddTransient<ICsvRecordStorage>(x =>
                {
                    var storage = x.GetRequiredService(serviceType);
                    return new CsvRecordStorage(
                        (string)getContainerName.GetValue(storage),
                        recordType,
                        AppendResultStorageService.CompactPrefix);
                });

                // Add the CSV compactor processor
                serviceCollection.AddTransient(
                    typeof(IMessageProcessor<>).MakeGenericType(typeof(CsvCompactMessage<>).MakeGenericType(recordType)),
                    typeof(CsvCompactorProcessor<>).MakeGenericType(recordType));
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
    }
}
