// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Kusto.Cloud.Platform.Utils;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.Worker.Workflow;

#nullable enable

namespace NuGet.Insights.Worker
{
    public class AllOutputCanBeReset : EndToEndTest
    {
        public AllOutputCanBeReset(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task Execute()
        {
            ConfigureSettings = x => ConfigureAllAuxiliaryFiles(x);

            // Initilize the workflow
            var workflowService = Host.Services.GetRequiredService<WorkflowService>();
            await workflowService.InitializeAsync();

            // Initialize all drivers
            var driverFactory = Host.Services.GetRequiredService<ICatalogScanDriverFactory>();
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var driver = driverFactory.Create(driverType);
                var descendingId = StorageUtility.GenerateDescendingId();
                var catalogIndexScan = new CatalogIndexScan(
                    driverType,
                    descendingId.ToString(),
                    descendingId.Unique);
                await driver.InitializeAsync();
                await driver.InitializeAsync(catalogIndexScan);
                await driver.FinalizeAsync(catalogIndexScan);
            }

            // Initialize all timers
            var timerExecutionService = Host.Services.GetRequiredService<SpecificTimerExecutionService>();
            var timers = Host.Services.GetServices<ITimer>();
            foreach (var timer in timers)
            {
                await timer.InitializeAsync();
                await timerExecutionService.ExecuteAsync([timer], executeNow: true);
            }

            // Add marker data to all existing tables and containers. The destroy step below should remove these.
            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync(Options.Value);
            var allTables = await tableServiceClient.QueryAsync().ToListAsync();
            foreach (var table in allTables)
            {
                if (!table.Name.StartsWith(StoragePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var tableClient = tableServiceClient.GetTableClient(table.Name);
                var entity = new TableEntity(nameof(AllOutputCanBeReset), table.Name);
                await tableClient.AddEntityAsync(entity);
            }

            var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync(Options.Value);
            var allContainers = await blobServiceClient.GetBlobContainersAsync().ToListAsync();
            foreach (var container in allContainers)
            {
                if (!container.Name.StartsWith(StoragePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var containerClient = blobServiceClient.GetBlobContainerClient(container.Name);
                var blob = containerClient.GetBlobClient(nameof(AllOutputCanBeReset));
                await blob.UploadAsync(Stream.Null);
            }

            // Get all table names and blob storage container names in the configuration
            var options = Options.Value;
            var properties = GetStorageNameProperties(options);
            var tables = properties.Where(x => x.StorageType == StorageType.Table).ToDictionary(x => x.Name, x => x.Value);
            var tablePrefixes = properties.Where(x => x.StorageType == StorageType.TablePrefix).ToDictionary(x => x.Name, x => x.Value);
            var blobContainers = properties.Where(x => x.StorageType == StorageType.Blob).ToDictionary(x => x.Name, x => x.Value);

            // Verify all table and blob storage container names are created
            foreach ((var key, var tableName) in tables)
            {
                Assert.True(await tableServiceClient.TableExistsAsync(tableName), $"The table for {key} ('{tableName}') should have been created.");
            }

            foreach ((var key, var containerName) in blobContainers)
            {
                var container = blobServiceClient.GetBlobContainerClient(containerName);
                Assert.True(await container.ExistsAsync(), $"The blob container for {key} ('{containerName}') should have been created.");
            }

            // Destroy output for all drivers and timers
            var destroyTasks = new List<Task>();
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var driver = driverFactory.Create(driverType);
                destroyTasks.Add(driver.DestroyOutputAsync());
            }

            foreach (var timer in timers)
            {
                if (timer.CanDestroy)
                {
                    destroyTasks.Add(timer.DestroyAsync());
                }
            }
            await Task.WhenAll(destroyTasks);

            // Remove non-output tables
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogIndexScanTableName)));
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.CursorTableName)));
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.KustoIngestionTableName)));
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.SingletonTaskStateTableName)));
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.TimedReprocessTableName)));
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.TimerTableName)));
            Assert.True(tables.Remove(nameof(NuGetInsightsWorkerSettings.WorkflowRunTableName)));

            // Remove non-output containers
            Assert.True(blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.LeaseContainerName)));

            // Verify all persistent output tables and blob containers are empty, and transient tables are deleted
            foreach ((var key, var tableName) in tables)
            {
                var table = tableServiceClient.GetTableClient(tableName);
                await WaitForAsync(() => IsEmptyAsync(table), $"The table for {key} ('{tableName}') should be empty.");
            }

            foreach ((var key, var containerName) in blobContainers)
            {
                var container = blobServiceClient.GetBlobContainerClient(containerName);
                await WaitForAsync(() => IsEmptyAsync(container), $"The blob container for {key} ('{containerName}') should be empty.");
            }

            foreach ((var key, var tableNamePrefix) in tablePrefixes)
            {
                await WaitForAsync(async () =>
                {
                    var existingTables = await tableServiceClient.QueryAsync(prefix: tableNamePrefix).ToListAsync();
                    return existingTables.Count == 0;
                }, $"The tables for {key} (starting with '{tableNamePrefix}') should be empty.");
            }
        }

        private async Task<bool> IsEmptyAsync(TableClientWithRetryContext table)
        {
            var entities = await table.QueryAsync<TableEntity>(x => true, maxPerPage: 1).Take(1).ToListAsync();
            return entities.Count == 0;
        }

        private async Task<bool> IsEmptyAsync(BlobContainerClient container)
        {
            var blobs = await container.GetBlobsAsync().Take(1).ToListAsync();
            return blobs.Count == 0;
        }

        private async Task WaitForAsync(Func<Task<bool>> isCompleteAsync, string message)
        {
            var sw = Stopwatch.StartNew();
            bool complete = false;
            Exception? lastException = null;
            while (sw.Elapsed < TimeSpan.FromSeconds(1 * 60))
            {
                try
                {
                    complete = await isCompleteAsync();
                    lastException = null;
                }
                catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    lastException = ex;
                    Logger.LogTransientWarning("{Message} HTTP 404 encountered. This problem should be transient.", message);
                }

                if (complete)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            if (lastException is not null)
            {
                Logger.LogError(lastException, "The exception never resolved.");
            }
            Assert.True(complete, message);
        }
    }
}
