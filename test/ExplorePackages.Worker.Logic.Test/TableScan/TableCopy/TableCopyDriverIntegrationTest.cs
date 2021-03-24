using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.LoadLatestPackageLeaf;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyDriverIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public TableCopyDriverIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        public class CopyAsync_Serial : TableCopyDriverIntegrationTest
        {
            public CopyAsync_Serial(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public Task ExecuteAsync()
            {
                return CopyAsync(TableScanStrategy.Serial);
            }
        }

        public class CopyAsync_PrefixScan : TableCopyDriverIntegrationTest
        {
            public CopyAsync_PrefixScan(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public Task ExecuteAsync()
            {
                return CopyAsync(TableScanStrategy.PrefixScan);
            }
        }

        private async Task CopyAsync(TableScanStrategy strategy)
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadLatestPackageLeaf, min0);
            await UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, onlyLatestLeaves: null, max1);

            var serviceClientFactory = Host.Services.GetRequiredService<NewServiceClientFactory>();
            var destTableName = StoragePrefix + "1d1";
            var tableServiceClient = await serviceClientFactory.GetTableServiceClientAsync();
            var sourceTable = tableServiceClient.GetTableClient(Options.Value.LatestPackageLeafTableName);
            var destinationTable = tableServiceClient.GetTableClient(destTableName);

            var tableScanService = Host.Services.GetRequiredService<TableScanService<LatestPackageLeaf>>();

            var taskStateStorageSuffix = "copy";
            await TaskStateStorageService.InitializeAsync(taskStateStorageSuffix);
            var taskState = await TaskStateStorageService.GetOrAddAsync(new TaskStateKey(taskStateStorageSuffix, "copy", "copy"));

            // Act
            await tableScanService.StartTableCopyAsync(
                taskState.Key,
                Options.Value.LatestPackageLeafTableName,
                destTableName,
                partitionKeyPrefix: string.Empty,
                strategy,
                takeCount: 10,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1);
            await UpdateAsync(taskState.Key);

            // Assert
            var sourceEntities = await sourceTable.QueryAsync<LatestPackageLeaf>().ToListAsync();
            var destinationEntities = await destinationTable.QueryAsync<LatestPackageLeaf>().ToListAsync();

            Assert.All(sourceEntities.Zip(destinationEntities), pair =>
            {
                pair.First.Timestamp = default;
                pair.First.ETag = default;
                pair.Second.Timestamp = default;
                pair.Second.ETag = default;
                Assert.Equal(JsonConvert.SerializeObject(pair.First), JsonConvert.SerializeObject(pair.Second));
            });

            var countLowerBound = await TaskStateStorageService.GetCountLowerBoundAsync(
                taskState.Key.StorageSuffix,
                taskState.Key.PartitionKey);
            Assert.Equal(0, countLowerBound);
        }
    }
}
