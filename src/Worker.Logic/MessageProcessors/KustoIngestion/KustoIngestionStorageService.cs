// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoIngestionStorageService> _logger;

        public KustoIngestionStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoIngestionStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await (await GetKustoIngestionTableAsync()).CreateIfNotExistsAsync(retry: true);
        }

        public async Task InitializeChildTableAsync(string storageSuffix)
        {
            await (await GetKustoIngestionTableAsync(storageSuffix)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteChildTableAsync(string storageSuffix)
        {
            await (await GetKustoIngestionTableAsync(storageSuffix)).DeleteAsync();
        }

        public async Task<bool> IsIngestionRunningAsync()
        {
            var table = await GetKustoIngestionTableAsync();
            var ingestions = await table
                .QueryAsync<KustoIngestionEntity>(x => x.PartitionKey == KustoIngestionEntity.DefaultPartitionKey)
                .ToListAsync();
            return ingestions.Any(x => !x.State.IsTerminal());
        }

        public async Task<KustoIngestionState?> GetLatestStateAsync()
        {
            var table = await GetKustoIngestionTableAsync();
            var ingestions = await table
                .QueryAsync<KustoIngestionEntity>(x => x.PartitionKey == KustoIngestionEntity.DefaultPartitionKey)
                .ToListAsync();
            return ingestions.OrderByDescending(x => x.Created).FirstOrDefault()?.State;
        }

        public async Task DeleteOldIngestionsAsync(string currentIngestionId)
        {
            var table = await GetKustoIngestionTableAsync();
            var oldIngestions = await table
                .QueryAsync<KustoIngestionEntity>(x => x.PartitionKey == KustoIngestionEntity.DefaultPartitionKey
                                                    && x.RowKey.CompareTo(currentIngestionId) > 0)
                .ToListAsync(_telemetryClient.StartQueryLoopMetrics());

            var oldIngestionsToDelete = oldIngestions
                .OrderByDescending(x => x.Created)
                .Skip(_options.Value.OldCatalogIndexScansToKeep)
                .OrderBy(x => x.Created)
                .Where(x => x.State.IsTerminal())
                .ToList();
            _logger.LogInformation("Deleting {Count} old Kusto ingestions.", oldIngestionsToDelete.Count);

            var batch = new MutableTableTransactionalBatch(table);
            foreach (var scan in oldIngestionsToDelete)
            {
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(table);
                }

                batch.DeleteEntity(scan.PartitionKey, scan.RowKey, scan.ETag);
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task<KustoIngestionEntity> GetIngestionAsync(string ingestionId)
        {
            var table = await GetKustoIngestionTableAsync();
            return await table.GetEntityOrNullAsync<KustoIngestionEntity>(KustoIngestionEntity.DefaultPartitionKey, ingestionId);
        }

        public async Task<IReadOnlyList<KustoIngestionEntity>> GetIngestionsAsync()
        {
            var table = await GetKustoIngestionTableAsync();
            var query = table.QueryAsync<KustoIngestionEntity>(filter: x => x.PartitionKey == KustoContainerIngestion.DefaultPartitionKey);
            return await query.ToListAsync();
        }

        public async Task<IReadOnlyList<KustoIngestionEntity>> GetLatestIngestionsAsync(int maxEntities)
        {
            return await (await GetKustoIngestionTableAsync())
                .QueryAsync<KustoIngestionEntity>(x => x.PartitionKey == KustoContainerIngestion.DefaultPartitionKey)
                .Take(maxEntities)
                .ToListAsync();
        }

        public async Task AddIngestionAsync(KustoIngestionEntity ingestion)
        {
            var table = await GetKustoIngestionTableAsync();
            var response = await table.AddEntityAsync(ingestion);
            ingestion.UpdateETag(response);
        }

        public async Task<KustoContainerIngestion> GetContainerAsync(string storageSuffix, string containerName)
        {
            var table = await GetKustoIngestionTableAsync(storageSuffix);
            return await table.GetEntityOrNullAsync<KustoContainerIngestion>(KustoContainerIngestion.DefaultPartitionKey, containerName);
        }

        public async Task<KustoBlobIngestion> GetBlobAsync(string storageSuffix, string containerName, string blobName)
        {
            var table = await GetKustoIngestionTableAsync(storageSuffix);
            return await table.GetEntityOrNullAsync<KustoBlobIngestion>(containerName, blobName);
        }

        public async Task ReplaceIngestionAsync(KustoIngestionEntity ingestion)
        {
            _logger.LogInformation(
                "Updating Kusto ingestion {IngestionId} with state {State}.",
                ingestion.IngestionId,
                ingestion.State);

            var table = await GetKustoIngestionTableAsync();
            var response = await table.UpdateEntityAsync(ingestion, ingestion.ETag, mode: TableUpdateMode.Replace);
            ingestion.UpdateETag(response);
        }

        public async Task ReplaceContainerAsync(KustoContainerIngestion container)
        {
            _logger.LogInformation(
                "Updating Kusto ingestion {IngestionId} for container {ContainerName} with state {State}.",
                container.IngestionId,
                container.ContainerName,
                container.State);

            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            var response = await table.UpdateEntityAsync(container, container.ETag, mode: TableUpdateMode.Replace);
            container.UpdateETag(response);
        }

        public async Task ReplaceContainersAsync(IReadOnlyList<KustoContainerIngestion> containers)
        {
            var storageSuffix = containers.Select(x => x.StorageSuffix).Distinct().Single();
            var table = await GetKustoIngestionTableAsync(storageSuffix);

            var batch = new MutableTableTransactionalBatch(table);
            foreach (var container in containers)
            {
                batch.UpdateEntity(container, container.ETag, TableUpdateMode.Replace);
            }

            await batch.SubmitBatchAsync();
        }

        public async Task ReplaceBlobAsync(KustoBlobIngestion blob)
        {
            _logger.LogInformation(
                "Updating Kusto ingestion {IngestionId} for blob {ContainerName}/{BlobName} with state {State}.",
                blob.IngestionId,
                blob.ContainerName,
                blob.BlobName,
                blob.State);

            var table = await GetKustoIngestionTableAsync(blob.StorageSuffix);
            var response = await table.UpdateEntityAsync(blob, blob.ETag, mode: TableUpdateMode.Replace);
            blob.UpdateETag(response);
        }

        public async Task DeleteContainerAsync(KustoContainerIngestion container)
        {
            _logger.LogInformation(
                "Deleting Kusto ingestion {IngestionId} for container {ContainerName}.",
                container.IngestionId,
                container.ContainerName);

            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            await table.DeleteEntityAsync(container, container.ETag);
        }

        public async Task DeleteBlobAsync(KustoBlobIngestion blob)
        {
            _logger.LogInformation(
                "Deleting Kusto ingestion {IngestionId} for blob {ContainerName}/{BlobName}.",
                blob.IngestionId,
                blob.ContainerName,
                blob.BlobName);

            var table = await GetKustoIngestionTableAsync(blob.StorageSuffix);
            await table.DeleteEntityAsync(blob, blob.ETag);
        }

        public async Task DeleteBlobsAsync(IReadOnlyList<KustoBlobIngestion> blobs)
        {
            var storageSuffix = blobs.Select(x => x.StorageSuffix).Distinct().Single();
            var table = await GetKustoIngestionTableAsync(storageSuffix);

            var batch = new MutableTableTransactionalBatch(table);
            foreach (var blob in blobs)
            {
                batch.DeleteEntity(blob.PartitionKey, blob.RowKey, blob.ETag);

                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(table);
                }
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task AddBlobsAsync(KustoContainerIngestion container, IReadOnlyList<KustoBlobIngestion> blobs)
        {
            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            var existingBlobs = await GetBlobsAsync(table, container);

            var existingBlobNames = existingBlobs.Select(x => x.BlobName).ToList();
            var missingBlobNames = blobs.Select(x => x.BlobName).Except(existingBlobNames).ToHashSet();
            var newBlobNames = blobs.Where(x => missingBlobNames.Contains(x.BlobName)).ToList();

            _logger.LogInformation(
                "Expanding {Count} blobs in Kusto ingestion {IngestionId} for container {ContainerName}.",
                newBlobNames.Count,
                container.IngestionId,
                container.ContainerName);

            var batch = new MutableTableTransactionalBatch(table);
            foreach (var blob in newBlobNames)
            {
                batch.AddEntity(blob);
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(table);
                }
            }
            await batch.SubmitBatchIfNotEmptyAsync();
        }

        public async Task<IReadOnlyList<KustoBlobIngestion>> GetBlobsAsync(KustoContainerIngestion container)
        {
            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            return await GetBlobsAsync(table, container);
        }

        private static async Task<List<KustoBlobIngestion>> GetBlobsAsync(TableClient table, KustoContainerIngestion container)
        {
            var query = table.QueryAsync<KustoBlobIngestion>(filter: x => x.PartitionKey == container.RowKey);
            return await query.ToListAsync();
        }

        public async Task AddContainersAsync(KustoIngestionEntity ingestion, IReadOnlyList<string> allContainerNames)
        {
            var table = await GetKustoIngestionTableAsync(ingestion.StorageSuffix);
            var containers = await GetContainersAsync(table, ingestion);

            var existingContainerNames = containers.Select(x => x.ContainerName).ToList();
            var missingContainerNames = allContainerNames.Except(existingContainerNames).ToList();
            if (existingContainerNames.Except(allContainerNames).Any())
            {
                throw new InvalidOperationException($"There are extra container names for ingestion '{ingestion.IngestionId}'.");
            }

            _logger.LogInformation(
                "Expanding {Count} containers in Kusto ingestion {IngestionId}.",
                missingContainerNames.Count,
                ingestion.IngestionId);

            if (missingContainerNames.Any())
            {
                var newContainers = missingContainerNames
                    .Select(x => new KustoContainerIngestion(x)
                    {
                        IngestionId = ingestion.IngestionId,
                        StorageSuffix = ingestion.StorageSuffix,
                        State = KustoContainerIngestionState.Created,
                    })
                    .ToList();

                var batch = new MutableTableTransactionalBatch(table);
                foreach (var container in newContainers)
                {
                    batch.AddEntity(container);
                }
                await batch.SubmitBatchAsync();
            }
        }

        public async Task<int> GetContainerCountLowerBoundAsync(string storageSuffix)
        {
            var table = await GetKustoIngestionTableAsync(storageSuffix);
            return await table.GetEntityCountLowerBoundAsync(
                KustoContainerIngestion.DefaultPartitionKey,
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<int> GetBlobCountLowerBoundAsync(string storageSuffix, string containerName)
        {
            var table = await GetKustoIngestionTableAsync(storageSuffix);
            return await table.GetEntityCountLowerBoundAsync(
                containerName,
                _telemetryClient.StartQueryLoopMetrics());
        }

        public async Task<IReadOnlyList<KustoContainerIngestion>> GetContainersAsync(KustoIngestionEntity ingestion)
        {
            var table = await GetKustoIngestionTableAsync(ingestion.StorageSuffix);
            return await GetContainersAsync(table, ingestion);
        }

        private static async Task<List<KustoContainerIngestion>> GetContainersAsync(TableClient table, KustoIngestionEntity ingestion)
        {
            var query = table.QueryAsync<KustoContainerIngestion>(filter: x => x.PartitionKey == KustoContainerIngestion.DefaultPartitionKey);
            return await query.ToListAsync();
        }

        private async Task<TableClient> GetKustoIngestionTableAsync()
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.KustoIngestionTableName);
        }

        private async Task<TableClient> GetKustoIngestionTableAsync(string storageSuffix)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(_options.Value.KustoIngestionTableName + storageSuffix);
        }
    }
}
