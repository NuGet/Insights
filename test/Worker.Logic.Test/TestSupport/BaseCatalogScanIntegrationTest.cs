// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.WideEntities;
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

        protected static SortedDictionary<string, List<string>> NormalizeHeaders(ILookup<string, string> headers)
        {
            // These headers are unstable
            var ignoredHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Accept-Ranges",
                "Access-Control-Allow-Origin",
                "Access-Control-Expose-Headers",
                "Age",
                "Cache-Control",
                "Date",
                "Expires",
                "Server",
                "Strict-Transport-Security",
                "X-Azure-Ref",
                "X-Azure-Ref-OriginShield",
                "X-Cache",
                "X-CDN-Rewrite",
                "X-Content-Type-Options",
                "x-ms-lease-state",
                "x-ms-request-id",
                "x-ms-version",
            };

            return new SortedDictionary<string, List<string>>(headers
                .Where(x => !ignoredHeaders.Contains(x.Key))
                .Select(grouping =>
                {
                    if (grouping.Key == "ETag")
                    {
                        var values = new List<string>();
                        foreach (var value in grouping)
                        {
                            if (!value.StartsWith("\""))
                            {
                                values.Add("\"" + value + "\"");
                            }
                            else
                            {
                                values.Add(value);
                            }
                        }

                        return values.GroupBy(x => grouping.Key).Single();
                    }
                    else
                    {
                        return grouping;
                    }
                })
                .ToDictionary(x => x.Key, x => x.ToList()));
        }

        protected async Task AssertEntityOutputAsync<T>(TableClient table, string dir, Action<T> cleanEntity = null) where T : class, ITableEntity, new()
        {
            var entities = await table.QueryAsync<T>().ToListAsync();

            // Workaround: https://github.com/Azure/azure-sdk-for-net/issues/21023
            var setTimestamp = typeof(T).GetProperty(nameof(ITableEntity.Timestamp));

            foreach (var entity in entities)
            {
                entity.ETag = default;
                setTimestamp.SetValue(entity, DateTimeOffset.MinValue);
                cleanEntity?.Invoke(entity);
            }

            var actual = SerializeTestJson(entities);
            var testDataFile = Path.Combine(TestData, dir, "entities.json");
            if (OverwriteTestData)
            {
                OverwriteTestDataAndCopyToSource(testDataFile, actual);
            }
            var expected = File.ReadAllText(testDataFile);
            Assert.Equal(expected, actual);
        }

        protected async Task AssertWideEntityOutputAsync<T>(string tableName, string dir, Func<Stream, T> deserializeEntity)
        {
            var service = Host.Services.GetRequiredService<WideEntityService>();

            var wideEntities = await service.RetrieveAsync(tableName);
            var entities = new List<(string PartitionKey, string RowKey, T Entity)>();
            foreach (var wideEntity in wideEntities)
            {
                var entity = deserializeEntity(wideEntity.GetStream());
                entities.Add((wideEntity.PartitionKey, wideEntity.RowKey, entity));
            }

            var actual = SerializeTestJson(entities.Select(x => new { x.PartitionKey, x.RowKey, x.Entity }));
            var testDataFile = Path.Combine(TestData, dir, "entities.json");
            if (OverwriteTestData)
            {
                OverwriteTestDataAndCopyToSource(testDataFile, actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, dir, "entities.json"));
            Assert.Equal(expected, actual);
        }

        protected void MakeDeletedPackageAvailable()
        {
            HttpMessageHandlerFactory.OnSendAsync = async req =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/behaviorsample.1.0.0.nupkg"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestData}/behaviorsample.1.0.0.nupkg.testdata");
                    var response = await TestDataHttpClient.SendAsync(newReq);
                    response.EnsureSuccessStatusCode();
                    return response;
                }

                return null;
            };

            var file = new FileInfo(Path.Combine(TestData, "behaviorsample.1.0.0.nupkg.testdata"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
            };
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
