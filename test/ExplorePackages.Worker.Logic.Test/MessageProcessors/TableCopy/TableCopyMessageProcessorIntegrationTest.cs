using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.FindLatestLeaves;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyMessageProcessorIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public TableCopyMessageProcessorIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        public class CopyAsync_Serial : TableCopyMessageProcessorIntegrationTest
        {
            public CopyAsync_Serial(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public Task ExecuteAsync() => CopyAsync(TableScanStrategy.Serial);
        }

        public class CopyAsync_PrefixScan : TableCopyMessageProcessorIntegrationTest
        {
            public CopyAsync_PrefixScan(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public Task ExecuteAsync() => CopyAsync(TableScanStrategy.PrefixScan);
        }

        private async Task CopyAsync(TableScanStrategy strategy)
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.FindLatestLeaves, min0);
            await UpdateAsync(CatalogScanDriverType.FindLatestLeaves, max1);

            var serviceClientFactory = Host.Services.GetRequiredService<ServiceClientFactory>();
            var destTableName = StoragePrefix + "1d1";
            var tableClient = serviceClientFactory.GetStorageAccount().CreateCloudTableClient();
            var sourceTable = tableClient.GetTableReference(Options.Value.LatestLeavesTableName);
            var destinationTable = tableClient.GetTableReference(destTableName);

            var tableScanService = Host.Services.GetRequiredService<TableScanService<LatestPackageLeaf>>();

            // Act
            await tableScanService.StartTableCopyAsync(
                sourceTable.Name,
                destinationTable.Name,
                partitionKeyPrefix: string.Empty,
                strategy,
                takeCount: 10);
            await ProcessQueueAsync();

            // Assert
            var sourceEntities = await sourceTable.GetEntitiesAsync<LatestPackageLeaf>(TelemetryClient.NewQueryLoopMetrics());
            var destinationEntities = await destinationTable.GetEntitiesAsync<LatestPackageLeaf>(TelemetryClient.NewQueryLoopMetrics());

            Assert.All(sourceEntities.Zip(destinationEntities), pair =>
            {
                pair.First.Timestamp = DateTimeOffset.MinValue;
                pair.First.ETag = string.Empty;
                pair.Second.Timestamp = DateTimeOffset.MinValue;
                pair.Second.ETag = string.Empty;
                Assert.Equal(JsonConvert.SerializeObject(pair.First), JsonConvert.SerializeObject(pair.Second));
            });
        }
    }
}
