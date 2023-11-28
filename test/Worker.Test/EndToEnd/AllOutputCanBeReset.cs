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
            var properties = options.GetType().GetProperties();
            SortedDictionary<string, string> GetNames(IEnumerable<PropertyInfo> properties)
            {
                return new SortedDictionary<string, string>(
                    properties.ToDictionary(x => x.Name, x => (string)x.GetValue(options)),
                    StringComparer.Ordinal);
            }
            var tables = GetNames(properties.Where(x => x.Name.EndsWith("TableName", StringComparison.Ordinal)));
            var blobContainers = GetNames(properties.Where(x => x.Name.EndsWith("ContainerName", StringComparison.Ordinal)));

            // Remove transient tables
            tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogLeafScanTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.CatalogPageScanTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.CsvRecordTableName));
            tables.Remove(nameof(NuGetInsightsWorkerSettings.VersionSetAggregateTableName));

            // Remove tables and containers for unsupported drivers
#if !ENABLE_CRYPTOAPI
                tables.Remove(nameof(NuGetInsightsWorkerSettings.CertificateToPackageTableName));
                tables.Remove(nameof(NuGetInsightsWorkerSettings.PackageToCertificateTableName));
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.CertificateContainerName));
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.PackageCertificateContainerName));
#endif

#if !ENABLE_NPE
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.NuGetPackageExplorerContainerName));
                blobContainers.Remove(nameof(NuGetInsightsWorkerSettings.NuGetPackageExplorerFileContainerName));
#endif

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
            tables.Remove(nameof(NuGetInsightsWorkerSettings.TaskStateTableName));
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
