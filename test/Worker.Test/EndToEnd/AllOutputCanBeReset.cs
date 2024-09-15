// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
            // Initialize all drivers and timers
            var driverFactory = Host.Services.GetRequiredService<ICatalogScanDriverFactory>();
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var driver = driverFactory.Create(driverType);
                var descendingId = StorageUtility.GenerateDescendingId();
                var catalogIndexScan = new CatalogIndexScan(
                    driverType,
                    descendingId.ToString(),
                    descendingId.Unique);
                await driver.InitializeAsync(catalogIndexScan);
                await driver.FinalizeAsync(catalogIndexScan);
            }

            var timers = Host.Services.GetServices<ITimer>();
            foreach (var timer in timers)
            {
                await timer.InitializeAsync();
            }

            // Get all table names and blob storage container names in the configuration
            var options = Options.Value;
            var properties = GetStorageNameProperties(options);
            var tables = properties.Where(x => x.StorageType == StorageType.Table).ToDictionary(x => x.Name, x => x.Value);
            var tablePrefixes = properties.Where(x => x.StorageType == StorageType.TablePrefix).ToDictionary(x => x.Name, x => x.Value);
            var blobContainers = properties.Where(x => x.StorageType == StorageType.Blob).ToDictionary(x => x.Name, x => x.Value);

            // Verify all table and blob storage container names are created
            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
            foreach ((var key, var tableName) in tables)
            {
                var table = tableServiceClient.GetTableClient(tableName);
                Assert.True(await table.ExistsAsync(), $"The table for {key} ('{tableName}') should have been created.");
            }

            var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync();
            foreach ((var key, var containerName) in blobContainers)
            {
                var container = blobServiceClient.GetBlobContainerClient(containerName);
                Assert.True(await container.ExistsAsync(), $"The blob container for {key} ('{containerName}') should have been created.");
            }

            // Destroy output for all drivers and timers
            foreach (var driverType in CatalogScanDriverMetadata.StartableDriverTypes)
            {
                var driver = driverFactory.Create(driverType);
                await driver.DestroyOutputAsync();
            }

            foreach (var timer in timers)
            {
                if (timer.CanDestroy)
                {
                    await timer.DestroyAsync();
                }
            }

            // Remove non-output tables
            tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogIndexScanTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.CursorTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.KustoIngestionTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.SingletonTaskStateTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.TimedReprocessTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.TimerTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.WorkflowRunTableName));

            // Remove non-output containers
            blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.LeaseContainerName));

            // Verify all out table and blob storage containers are deleted
            foreach ((var key, var tableName) in tables)
            {
                var table = tableServiceClient.GetTableClient(tableName);
                Assert.True(await WaitForAsync(async () => !await table.ExistsAsync()), $"The table for {key} ('{tableName}') should have been deleted.");
            }

            foreach ((var key, var tableNamePrefix) in tablePrefixes)
            {
                Assert.True(await WaitForAsync(async () =>
                {
                    var existingTables = await tableServiceClient.QueryAsync(prefix: tableNamePrefix).ToListAsync();
                    return existingTables.Count == 0;
                }), $"The tables for {key} (starting with '{tableNamePrefix}') should have been deleted.");
            }

            foreach ((var key, var containerName) in blobContainers)
            {
                var container = blobServiceClient.GetBlobContainerClient(containerName);
                Assert.True(await WaitForAsync(async () => !await container.ExistsAsync()), $"The blob container for {key} ('{containerName}') should have been deleted.");
            }
        }

        private static async Task<bool> WaitForAsync(Func<Task<bool>> isCompleteAsync)
        {
            var sw = Stopwatch.StartNew();
            bool complete;
            while (!(complete = await isCompleteAsync()) && sw.Elapsed < TimeSpan.FromSeconds(5 * 60))
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            return complete;
        }
    }
}
