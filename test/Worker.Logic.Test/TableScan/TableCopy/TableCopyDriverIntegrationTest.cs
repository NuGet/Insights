// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadLatestPackageLeaf;

namespace NuGet.Insights.Worker.TableCopy
{
    public class TableCopyDriverIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public TableCopyDriverIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        [Fact]
        public Task CopyAsync_Serial()
        {
            return CopyAsync(TableScanStrategy.Serial, lowerBound: null, upperBound: null);
        }

        [Fact]
        public Task CopyAsync_PrefixScan()
        {
            return CopyAsync(TableScanStrategy.PrefixScan, lowerBound: null, upperBound: null);
        }

        [Fact]
        public Task CopyAsync_PrefixScan_WithBounds()
        {
            return CopyAsync(TableScanStrategy.PrefixScan, lowerBound: "dexih.connections.bbbbb", upperBound: "uuuuu");
        }

        private async Task CopyAsync(TableScanStrategy strategy, string lowerBound, string upperBound)
        {
            // Arrange
            ConfigureWorkerSettings = x => x.TableScanTakeCount = 10;
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadLatestPackageLeaf, min0);
            await UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, onlyLatestLeaves: null, max1);

            var serviceClientFactory = Host.Services.GetRequiredService<ServiceClientFactory>();
            var destTableName = StoragePrefix + "1d1";
            var tableServiceClient = await serviceClientFactory.GetTableServiceClientAsync(Options.Value);
            var sourceTable = tableServiceClient.GetTableClient(Options.Value.LatestPackageLeafTableName);
            var destinationTable = tableServiceClient.GetTableClient(destTableName);

            var tableScanService = Host.Services.GetRequiredService<TableScanService>();

            var taskStateStorageSuffix = "copy";
            var taskStateKey = new TaskStateKey(taskStateStorageSuffix, "copy", "copy");
            await tableScanService.InitializeTaskStateAsync(taskStateKey);

            // Act
            await tableScanService.StartTableCopyAsync<LatestPackageLeaf>(
                taskStateKey,
                Options.Value.LatestPackageLeafTableName,
                destTableName,
                partitionKeyPrefix: string.Empty,
                partitionKeyLowerBound: lowerBound,
                partitionKeyUpperBound: upperBound,
                strategy,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1);
            await UpdateAsync(taskStateKey);

            // Assert
            var allSourceEntities = (await sourceTable.QueryAsync<LatestPackageLeaf>().ToListAsync()).ToList();
            var sourceEntities = allSourceEntities
                .Where(x => lowerBound is null || string.CompareOrdinal(x.PartitionKey, lowerBound) > 0)
                .Where(x => upperBound is null || string.CompareOrdinal(x.PartitionKey, upperBound) < 0)
                .ToList();
            var destinationEntities = await destinationTable.QueryAsync<LatestPackageLeaf>().ToListAsync();

            if (lowerBound is not null || upperBound is not null)
            {
                Assert.True(allSourceEntities.Count > sourceEntities.Count);
            }

            Assert.Equal(sourceEntities.Count, destinationEntities.Count);
            Assert.All(sourceEntities.Zip(destinationEntities), pair =>
            {
                pair.First.Timestamp = default;
                pair.First.ETag = default;
                pair.First.ClientRequestId = default;
                pair.Second.Timestamp = default;
                pair.Second.ETag = default;
                pair.Second.ClientRequestId = default;
                Assert.Equal(JsonSerializer.Serialize(pair.First), JsonSerializer.Serialize(pair.Second));
            });

            Assert.True(await tableScanService.IsCompleteAsync(taskStateKey.StorageSuffix, taskStateKey.PartitionKey));
        }
    }
}
