using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
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

        protected async Task VerifyOutputAsync<T>(CloudTable table, string dir, Action<T> cleanEntity = null) where T : ITableEntity, new()
        {
            var entities = await table.GetEntitiesAsync<T>(TelemetryClient.StartQueryLoopMetrics());

            foreach (var entity in entities)
            {
                entity.ETag = null;
                entity.Timestamp = DateTimeOffset.MinValue;
                cleanEntity?.Invoke(entity);
            }

            var serializerSettings = NameVersionSerializer.JsonSerializerSettings;
            serializerSettings.NullValueHandling = NullValueHandling.Include;
            serializerSettings.Formatting = Formatting.Indented;
            var actual = JsonConvert.SerializeObject(entities, serializerSettings);
            if (OverwriteTestData)
            {
                Directory.CreateDirectory(Path.Combine(TestData, dir));
                File.WriteAllText(Path.Combine(TestData, dir, "entities.json"), actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, dir, "entities.json"));
            Assert.Equal(expected, actual);

            await VerifyExpectedStorageAsync();
        }

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
                GetExpectedCursorNames().OrderBy(x => x).ToArray(),
                cursors.Select(x => x.RowKey).ToArray());

            var catalogIndexScans = await tableClient
                .GetTableReference(Options.Value.CatalogIndexScanTableName)
                .GetEntitiesAsync<CatalogIndexScan>(TelemetryClient.StartQueryLoopMetrics());
            Assert.Equal(
                ExpectedCatalogIndexScans.Select(x => (x.PartitionKey, x.RowKey)).OrderBy(x => x).ToArray(),
                catalogIndexScans.Select(x => (x.PartitionKey, x.RowKey)).ToArray());

        }

        protected virtual IEnumerable<string> GetExpectedCursorNames()
        {
            yield return $"CatalogScan-{DriverType}";
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
