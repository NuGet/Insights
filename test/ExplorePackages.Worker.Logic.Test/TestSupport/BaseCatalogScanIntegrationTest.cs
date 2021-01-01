using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseCatalogScanIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public BaseCatalogScanIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract CatalogScanDriverType DriverType { get; }

        protected Task SetCursorAsync(DateTimeOffset min) => SetCursorAsync(DriverType, min);

        protected virtual Task<CatalogIndexScan> UpdateAsync(DateTimeOffset max) => UpdateAsync(DriverType, onlyLatestLeaves: null, max);

        protected async Task VerifyExpectedStorageAsync()
        {
            var account = ServiceClientFactory.GetStorageAccount();

            var blobClient = account.CreateCloudBlobClient();
            var queueClient = account.CreateCloudQueueClient();
            var tableClient = account.CreateCloudTableClient();

            var containers = await blobClient.ListContainersAsync(StoragePrefix);
            Assert.Equal(
                GetExpectedBlobContainerNames().Concat(new[] { Options.Value.LeaseContainerName }).OrderBy(x => x).ToArray(),
                containers.Select(x => x.Name).ToArray());

            var leaseBlobs = await blobClient
                .GetContainerReference(Options.Value.LeaseContainerName)
                .ListBlobsAsync(TelemetryClient.StartQueryLoopMetrics());
            Assert.Equal(
                new[] { $"Start-CatalogScan-{DriverType}" }.Concat(GetExpectedLeaseNames()).OrderBy(x => x).ToArray(),
                leaseBlobs.Select(x => x.Name).ToArray());

            var queues = await queueClient.ListQueuesAsync(StoragePrefix);
            Assert.Equal(
                new[] { Options.Value.WorkerQueueName, Options.Value.WorkerQueueName + "-poison" },
                queues.Select(x => x.Name).ToArray());

            foreach (var queue in queues)
            {
                await queue.FetchAttributesAsync();
                Assert.Equal(0, queue.ApproximateMessageCount);
            }

            var tables = await tableClient.ListTablesAsync(StoragePrefix);
            Assert.Equal(
                GetExpectedTableNames().Concat(new[] { Options.Value.CursorTableName, Options.Value.CatalogIndexScanTableName }).OrderBy(x => x).ToArray(),
                tables.Select(x => x.Name).ToArray());

            var cursors = await tableClient
                .GetTableReference(Options.Value.CursorTableName)
                .GetEntitiesAsync<CursorTableEntity>(TelemetryClient.StartQueryLoopMetrics());
            Assert.Equal(
                new[] { $"CatalogScan-{DriverType}" },
                cursors.Select(x => x.RowKey).ToArray());

            var catalogIndexScans = await tableClient
                .GetTableReference(Options.Value.CatalogIndexScanTableName)
                .GetEntitiesAsync<CatalogIndexScan>(TelemetryClient.StartQueryLoopMetrics());
            Assert.Equal(
                UpdatedCatalogIndexScans.Select(x => (x.PartitionKey, x.RowKey)).OrderBy(x => x).ToArray(),
                catalogIndexScans.Select(x => (x.PartitionKey, x.RowKey)).ToArray());

        }

        protected virtual IEnumerable<string> GetExpectedLeaseNames()
        {
            yield break;
        }

        protected virtual IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield break;
        }

        protected virtual IEnumerable<string> GetExpectedTableNames()
        {
            yield break;
        }
    }
}
