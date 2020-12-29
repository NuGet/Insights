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
    public class TableCopyMessageProcessorIntegrationTest : BaseWorkerIntegrationTest
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
            public Task ExecuteAsync() => CopyAsync(TableCopyStrategy.Serial);
        }

        public class CopyAsync_PrefixScan : TableCopyMessageProcessorIntegrationTest
        {
            public CopyAsync_PrefixScan(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public Task ExecuteAsync() => CopyAsync(TableCopyStrategy.PrefixScan);
        }

        private async Task CopyAsync(TableCopyStrategy strategy)
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T20:58:24.1558179Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T23:41:30.2461308Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanType.FindLatestLeaves, min0);
            await UpdateAsync(CatalogScanType.FindLatestLeaves, max1);

            var serviceClientFactory = Host.Services.GetRequiredService<ServiceClientFactory>();
            var destTableName = StoragePrefix + "1d1";
            var tableClient = serviceClientFactory.GetStorageAccount().CreateCloudTableClient();
            var sourceTable = tableClient.GetTableReference(Options.Value.LatestLeavesTableName);
            var destinationTable = tableClient.GetTableReference(destTableName);
            await destinationTable.CreateIfNotExistsAsync();

            var enqueuer = Host.Services.GetRequiredService<TableCopyEnqueuer<LatestPackageLeaf>>();

            // Act
            switch (strategy)
            {
                case TableCopyStrategy.Serial:
                    await enqueuer.StartSerialAsync(sourceTable.Name, destinationTable.Name);
                    break;
                case TableCopyStrategy.PrefixScan:
                    await enqueuer.StartPrefixScanAsync(sourceTable.Name, destinationTable.Name, partitionKeyPrefix: "", takeCount: 10);
                    break;
                default:
                    throw new NotImplementedException();
            }
            await ProcessQueueAsync();

            // Assert
            var sourceEntities = await sourceTable.GetEntitiesAsync<LatestPackageLeaf>();
            var destinationEntities = await destinationTable.GetEntitiesAsync<LatestPackageLeaf>();

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
