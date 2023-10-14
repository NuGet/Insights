// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using Xunit;
using Xunit.Abstractions;

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
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadLatestPackageLeaf, min0);
            await UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, onlyLatestLeaves: null, max1);

            var serviceClientFactory = Host.Services.GetRequiredService<ServiceClientFactory>();
            var destTableName = StoragePrefix + "1d1";
            var tableServiceClient = await serviceClientFactory.GetTableServiceClientAsync();
            var sourceTable = tableServiceClient.GetTableClient(Options.Value.LatestPackageLeafTableName);
            var destinationTable = tableServiceClient.GetTableClient(destTableName);

            var tableScanService = Host.Services.GetRequiredService<TableScanService<LatestPackageLeaf>>();

            var taskStateStorageSuffix = "copy";
            await TaskStateStorageService.InitializeAsync(taskStateStorageSuffix);
            var taskStateKey = new TaskStateKey(taskStateStorageSuffix, "copy", "copy");
            await TaskStateStorageService.AddAsync(taskStateKey);

            // Act
            await tableScanService.StartTableCopyAsync(
                taskStateKey,
                Options.Value.LatestPackageLeafTableName,
                destTableName,
                partitionKeyPrefix: string.Empty,
                partitionKeyLowerBound: lowerBound,
                partitionKeyUpperBound: upperBound,
                strategy,
                takeCount: 10,
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

            Assert.All(sourceEntities.Zip(destinationEntities), pair =>
            {
                pair.First.Timestamp = default;
                pair.First.ETag = default;
                pair.Second.Timestamp = default;
                pair.Second.ETag = default;
                Assert.Equal(JsonSerializer.Serialize(pair.First), JsonSerializer.Serialize(pair.Second));
            });

            var countLowerBound = await TaskStateStorageService.GetCountLowerBoundAsync(
                taskStateKey.StorageSuffix,
                taskStateKey.PartitionKey);
            Assert.Equal(0, countLowerBound);
        }
    }
}
