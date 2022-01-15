// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Xunit;
using Xunit.Abstractions;

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

        public override async Task DisposeAsync()
        {
            try
            {
                // Global assertions
                await AssertExpectedStorageAsync();
            }
            finally
            {
                // Clean up
                await base.DisposeAsync();
            }
        }

        private async Task AssertExpectedStorageAsync()
        {
            var blobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync();
            var queueServiceClient = await ServiceClientFactory.GetQueueServiceClientAsync();
            var tableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();

            var containers = await blobServiceClient.GetBlobContainersAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                GetExpectedBlobContainerNames().Concat(new[] { Options.Value.LeaseContainerName }).OrderBy(x => x).ToArray(),
                containers.Select(x => x.Name).ToArray());

            var leaseBlobs = await blobServiceClient
                .GetBlobContainerClient(Options.Value.LeaseContainerName)
                .GetBlobsAsync()
                .ToListAsync();
            var expectedLeaseNames = GetExpectedLeaseNames().OrderBy(x => x).ToArray();
            var actualLeaseNames = leaseBlobs.Select(x => x.Name).ToArray();
            Assert.Equal(expectedLeaseNames, actualLeaseNames);

            var queueItems = await queueServiceClient.GetQueuesAsync(prefix: StoragePrefix).ToListAsync();
            Assert.Equal(
                new[]
                {
                    Options.Value.ExpandQueueName,
                    Options.Value.ExpandQueueName + "-poison",
                    Options.Value.WorkQueueName,
                    Options.Value.WorkQueueName + "-poison",
                },
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
                GetExpectedTableNames().Concat(new[] { Options.Value.CursorTableName, Options.Value.CatalogIndexScanTableName }).OrderBy(x => x).ToArray(),
                tables.Select(x => x.Name).ToArray());

            var cursors = await tableServiceClient
                .GetTableClient(Options.Value.CursorTableName)
                .QueryAsync<CursorTableEntity>()
                .ToListAsync();
            Assert.Equal(
                GetExpectedCursorNames().OrderBy(x => x).ToArray(),
                cursors.Select(x => x.GetName()).ToArray());

            var catalogIndexScans = await tableServiceClient
                .GetTableClient(Options.Value.CatalogIndexScanTableName)
                .QueryAsync<CatalogIndexScan>()
                .ToListAsync();
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
            yield return $"Start-CatalogScan-{DriverType}";

            foreach (var type in LatestLeavesTypes)
            {
                foreach (var scan in ExpectedCatalogIndexScans.Where(x => x.DriverType == type))
                {
                    yield return $"Start-CatalogScan-{CatalogScanDriverType.Internal_FindLatestCatalogLeafScan}-{scan.GetScanId()}-fl";
                }
            }

            foreach (var type in LatestLeavesPerIdTypes)
            {
                foreach (var scan in ExpectedCatalogIndexScans.Where(x => x.DriverType == type))
                {
                    yield return $"Start-CatalogScan-{CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId}-{scan.GetScanId()}-fl";
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
