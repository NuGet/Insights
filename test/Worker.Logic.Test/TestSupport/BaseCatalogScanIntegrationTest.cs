// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogScanIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public BaseCatalogScanIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract CatalogScanDriverType DriverType { get; }
        public abstract IEnumerable<CatalogScanDriverType> LatestLeavesTypes { get; }
        public abstract IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes { get; }

        protected Task SetCursorAsync(DateTimeOffset min)
        {
            return SetCursorAsync(DriverType, min);
        }

        protected virtual Task<CatalogIndexScan> UpdateAsync(DateTimeOffset max)
        {
            return UpdateAsync(DriverType, onlyLatestLeaves: null, max);
        }

        protected override async Task DisposeInternalAsync()
        {
            await AssertExpectedStorageAsync();
            await base.DisposeInternalAsync();
        }

        private async Task AssertExpectedStorageAsync()
        {
            var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync();
            var queueServiceClient = await ServiceClientFactory.GetQueueServiceClientAsync();
            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();

            var containers = await blobServiceClient.GetBlobContainersAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                GetExpectedBlobContainerNames().Concat(new[] { Options.Value.LeaseContainerName }).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                containers.Select(x => x.Name).ToArray());

            var leaseBlobs = await blobServiceClient
                .GetBlobContainerClient(Options.Value.LeaseContainerName)
                .GetBlobsAsync()
                .ToListAsync();
            var expectedLeaseNames = GetExpectedLeaseNames().Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var actualLeaseNames = leaseBlobs.Select(x => x.Name).ToArray();
            Assert.Equal(expectedLeaseNames, actualLeaseNames);

            var queueItems = await queueServiceClient.GetQueuesAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                new[]
                {
                    Options.Value.WorkQueueName,
                    Options.Value.WorkQueueName + "-poison",
                    Options.Value.ExpandQueueName,
                    Options.Value.ExpandQueueName + "-poison",
                }.Order().ToArray(),
                queueItems.Select(x => x.Name).ToArray());

            foreach (var queueItem in queueItems)
            {
                var queueClient = (await ServiceClientFactory.GetQueueServiceClientAsync())
                    .GetQueueClient(queueItem.Name);
                QueueProperties properties = await queueClient.GetPropertiesAsync();
                Assert.Equal(0, properties.ApproximateMessagesCount);
            }

            var tables = await tableServiceClient.QueryAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                GetExpectedTableNames()
                    .Concat(new[] { Options.Value.CursorTableName, Options.Value.CatalogIndexScanTableName })
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray(),
                tables.Select(x => x.Name).ToArray());

            var cursors = await tableServiceClient
                .GetTableClient(Options.Value.CursorTableName)
                .QueryAsync<CursorTableEntity>()
                .ToListAsync();
            Assert.Equal(
                GetExpectedCursorNames()
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray(),
                cursors.Select(x => x.Name).ToArray());

            var catalogIndexScans = await tableServiceClient
                .GetTableClient(Options.Value.CatalogIndexScanTableName)
                .QueryAsync<CatalogIndexScan>()
                .ToListAsync();
            Assert.Equal(
                ExpectedCatalogIndexScans
                    .Select(x => (x.PartitionKey, x.RowKey))
                    .OrderBy(x => x.PartitionKey, StringComparer.Ordinal)
                    .ThenBy(x => x.RowKey, StringComparer.Ordinal).ToArray(),
                catalogIndexScans.Select(x => (x.PartitionKey, x.RowKey)).ToArray());
        }

        protected virtual IEnumerable<string> GetExpectedCursorNames()
        {
            yield return $"CatalogScan-{DriverType}";
        }

        protected virtual IEnumerable<string> GetExpectedLeaseNames()
        {
            yield return $"Start-{DriverType}";

            foreach (var type in LatestLeavesTypes)
            {
                foreach (var scan in ExpectedCatalogIndexScans.Where(x => x.DriverType == type))
                {
                    yield return $"Start-{CatalogScanDriverType.Internal_FindLatestCatalogLeafScan}-{scan.DriverType}";
                }
            }

            foreach (var type in LatestLeavesPerIdTypes)
            {
                foreach (var scan in ExpectedCatalogIndexScans.Where(x => x.DriverType == type))
                {
                    yield return $"Start-{CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId}-{scan.DriverType}";
                }
            }
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
