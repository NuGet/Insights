using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Knapcode.ExplorePackages.Worker.BuildVersionSet;
using Knapcode.ExplorePackages.Worker.KustoIngestion;
using Kusto.Data.Common;
using Kusto.Ingest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseWorkerLogicIntegrationTest : BaseLogicIntegrationTest
    {
        protected BaseWorkerLogicIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            // Version set
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(Array.Empty<string>());
            MockVersionSet.Setup(x => x.GetUncheckedVersions(It.IsAny<string>())).Returns(Array.Empty<string>());
            MockVersionSetProvider.Setup(x => x.GetAsync()).ReturnsAsync(() => MockVersionSet.Object);

            // Kusto SDK
            MockCslAdminProvider = new Mock<ICslAdminProvider>();
            MockKustoQueueIngestClient = new Mock<IKustoQueuedIngestClient>();
            MockKustoQueueIngestClient
                .Setup(x => x.IngestFromStorageAsync(
                    It.IsAny<string>(),
                    It.IsAny<KustoIngestionProperties>(),
                    It.IsAny<StorageSourceOptions>()))
                .Returns<string, KustoIngestionProperties, StorageSourceOptions>(async (u, p, o) =>
                {
                    var account = CloudStorageAccount.Parse(TestSettings.StorageConnectionString);
                    var client = account.CreateCloudTableClient();
                    var writeTable = client.GetTableReference(StoragePrefix + "1kir1");
                    await writeTable.CreateIfNotExistsAsync();
                    await writeTable.ExecuteAsync(TableOperation.Insert(new IngestionStatus(o.SourceId)
                    {
                        Status = Status.Succeeded,
                        UpdatedOn = DateTime.UtcNow,
                    }));

                    string sas;
                    if (account.Credentials.IsSAS)
                    {
                        sas = account.Credentials.SASToken;
                    }
                    else
                    {
                        sas = account.GetSharedAccessSignature(new SharedAccessAccountPolicy
                        {
                            Permissions = SharedAccessAccountPermissions.Read,
                            ResourceTypes = SharedAccessAccountResourceTypes.Object,
                            Services = SharedAccessAccountServices.Table,
                            SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(7),
                        });
                    }

                    var tableUri = new UriBuilder(writeTable.Uri) { Query = sas };
                    var readTable = new CloudTable(tableUri.Uri);
                    return new TableReportIngestionResult(readTable);
                });
        }

        public Action<ExplorePackagesWorkerSettings> ConfigureWorkerSettings { get; set; }
        public IOptions<ExplorePackagesWorkerSettings> Options => Host.Services.GetRequiredService<IOptions<ExplorePackagesWorkerSettings>>();
        public CatalogScanService CatalogScanService => Host.Services.GetRequiredService<CatalogScanService>();
        public CatalogScanCursorService CatalogScanCursorService => Host.Services.GetRequiredService<CatalogScanCursorService>();
        public CursorStorageService CursorStorageService => Host.Services.GetRequiredService<CursorStorageService>();
        public CatalogScanStorageService CatalogScanStorageService => Host.Services.GetRequiredService<CatalogScanStorageService>();
        public TaskStateStorageService TaskStateStorageService => Host.Services.GetRequiredService<TaskStateStorageService>();
        public KustoIngestionService KustoIngestionService => Host.Services.GetRequiredService<KustoIngestionService>();
        public KustoIngestionStorageService KustoIngestionStorageService => Host.Services.GetRequiredService<KustoIngestionStorageService>();
        public IMessageEnqueuer MessageEnqueuer => Host.Services.GetRequiredService<IMessageEnqueuer>();
        public IWorkerQueueFactory WorkerQueueFactory => Host.Services.GetRequiredService<IWorkerQueueFactory>();

        public Mock<IVersionSetProvider> MockVersionSetProvider { get; } = new Mock<IVersionSetProvider>();
        public Mock<IVersionSet> MockVersionSet { get; } = new Mock<IVersionSet>();
        public Mock<ICslAdminProvider> MockCslAdminProvider { get; }
        public Mock<IKustoQueuedIngestClient> MockKustoQueueIngestClient { get; }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddExplorePackagesWorker();
                serviceCollection.Configure((Action<ExplorePackagesWorkerSettings>)ConfigureWorkerDefaultsAndSettings);
            });
        }

        protected void ConfigureWorkerDefaultsAndSettings(ExplorePackagesWorkerSettings x)
        {
            x.AppendResultStorageBucketCount = 3;
            x.KustoDatabaseName = "TestKustoDb";

            x.WorkQueueName = $"{StoragePrefix}1wq1";
            x.ExpandQueueName = $"{StoragePrefix}1eq1";
            x.CursorTableName = $"{StoragePrefix}1c1";
            x.CatalogIndexScanTableName = $"{StoragePrefix}1cis1";
            x.CatalogPageScanTableName = $"{StoragePrefix}1cps1";
            x.CatalogLeafScanTableName = $"{StoragePrefix}1cls1";
            x.TaskStateTableName = $"{StoragePrefix}1ts1";
            x.CsvRecordTableName = $"{StoragePrefix}1cr1";
            x.VersionSetAggregateTableName = $"{StoragePrefix}1vsa1";
            x.VersionSetContainerName = $"{StoragePrefix}1vs1";
            x.KustoIngestionTableName = $"{StoragePrefix}1ki1";
            x.LatestPackageLeafTableName = $"{StoragePrefix}1lpl1";
            x.PackageVersionTableName = $"{StoragePrefix}1pv1";
            x.PackageVersionContainerName = $"{StoragePrefix}1pvc1";
            x.PackageAssetContainerName = $"{StoragePrefix}1fpa1";
            x.PackageAssemblyContainerName = $"{StoragePrefix}1fpi1";
            x.PackageManifestContainerName = $"{StoragePrefix}1pm2c1";
            x.PackageSignatureContainerName = $"{StoragePrefix}1fps1";
            x.CatalogLeafItemContainerName = $"{StoragePrefix}1fcli1";
            x.PackageDownloadsContainerName = $"{StoragePrefix}1pd1";
            x.PackageOwnersContainerName = $"{StoragePrefix}1po1";
            x.PackageArchiveContainerName = $"{StoragePrefix}1pa2c1";
            x.PackageArchiveEntryContainerName = $"{StoragePrefix}1pae2c1";
            x.NuGetPackageExplorerContainerName = $"{StoragePrefix}1npe2c1";
            x.NuGetPackageExplorerFileContainerName = $"{StoragePrefix}1npef2c1";

            ConfigureDefaultsAndSettings(x);

            if (ConfigureWorkerSettings != null)
            {
                ConfigureWorkerSettings(x);
            }

            AssertStoragePrefix(x);
        }

        protected async Task SetCursorAsync(CatalogScanDriverType driverType, DateTimeOffset min)
        {
            var cursor = await CatalogScanCursorService.GetCursorAsync(driverType);
            cursor.Value = min;
            await CursorStorageService.UpdateAsync(cursor);
        }

        public ConcurrentBag<CatalogIndexScan> ExpectedCatalogIndexScans { get; } = new ConcurrentBag<CatalogIndexScan>();

        protected async Task<CatalogIndexScan> UpdateAsync(CatalogScanDriverType driverType, bool? onlyLatestLeaves, DateTimeOffset max)
        {
            var result = await CatalogScanService.UpdateAsync(driverType, max, onlyLatestLeaves);
            return await UpdateAsync(result.Scan);
        }

        protected async Task<KustoIngestionEntity> UpdateAsync(KustoIngestionEntity ingestion)
        {
            Assert.NotNull(ingestion);
            await ProcessQueueAsync(() => { }, async () =>
            {
                ingestion = await KustoIngestionStorageService.GetIngestionAsync(ingestion.GetIngestionId());

                if (ingestion.State != KustoIngestionState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    return false;
                }

                return true;
            });

            return ingestion;
        }

        protected async Task<CatalogIndexScan> UpdateAsync(CatalogIndexScan indexScan)
        {
            Assert.NotNull(indexScan);
            await ProcessQueueAsync(() => { }, async () =>
            {
                indexScan = await CatalogScanStorageService.GetIndexScanAsync(indexScan.GetCursorName(), indexScan.GetScanId());

                if (indexScan.State != CatalogIndexScanState.Complete)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    return false;
                }

                return true;
            });

            ExpectedCatalogIndexScans.Add(indexScan);

            return indexScan;
        }

        protected async Task UpdateAsync(TaskStateKey taskStateKey)
        {
            await ProcessQueueAsync(() => { }, async () =>
            {
                var countLowerBound = await TaskStateStorageService.GetCountLowerBoundAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey);
                if (countLowerBound > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    return false;
                }

                return true;
            });
        }

        protected async Task ProcessQueueAsync(Action foundMessage, Func<Task<bool>> isCompleteAsync)
        {
            var expandQueue = await WorkerQueueFactory.GetQueueAsync(QueueType.Expand);
            var workerQueue = await WorkerQueueFactory.GetQueueAsync(QueueType.Work);

            async Task<(QueueType queueType, QueueClient queue, QueueMessage message)> ReceiveMessageAsync()
            {
                QueueMessage message = await expandQueue.ReceiveMessageAsync();
                if (message != null)
                {
                    return (QueueType.Expand, expandQueue, message);
                }

                message = await workerQueue.ReceiveMessageAsync();
                if (message != null)
                {
                    return (QueueType.Work, workerQueue, message);
                }

                return (QueueType.Work, null, null);
            };

            bool isComplete;
            do
            {
                while (true)
                {
                    (var queueType, var queue, var message) = await ReceiveMessageAsync();
                    if (message != null)
                    {
                        foundMessage();
                        using (var scope = Host.Services.CreateScope())
                        {
                            await ProcessMessageAsync(scope.ServiceProvider, queueType, message);
                        }

                        await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                    }
                    else
                    {
                        break;
                    }
                }

                isComplete = await isCompleteAsync();
            }
            while (!isComplete);
        }


        protected virtual async Task ProcessMessageAsync(IServiceProvider serviceProvider, QueueType queue, QueueMessage message)
        {
            var leaseScope = serviceProvider.GetRequiredService<TempStreamLeaseScope>();
            await using var scopeOwnership = leaseScope.TakeOwnership();
            var messageProcessor = serviceProvider.GetRequiredService<IGenericMessageProcessor>();
            await messageProcessor.ProcessSingleAsync(QueueType.Work, message.Body.ToString(), message.DequeueCount);
        }

        protected async Task AssertCompactAsync<T>(string containerName, string testName, string stepName, int bucket) where T : ICsvRecord
        {
            await AssertCsvBlobAsync<T>(containerName, testName, stepName, $"compact_{bucket}.csv.gz");
        }

        private class TableReportIngestionResult : IKustoIngestionResult
        {
            public TableReportIngestionResult(CloudTable ingestionStatusTable)
            {
                IngestionStatusTable = ingestionStatusTable;
            }

            public CloudTable IngestionStatusTable { get; }

            public IngestionStatus GetIngestionStatusBySourceId(Guid sourceId)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<IngestionStatus> GetIngestionStatusCollection()
            {
                throw new NotImplementedException();
            }
        }
    }
}
