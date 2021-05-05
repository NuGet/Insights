using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.KustoIngestion
{
    public class KustoIngestionStorageService
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public KustoIngestionStorageService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
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
            await (await GetKustoIngestionTableAsync(storageSuffix)).DeleteIfExistsAsync();
        }

        public async Task<IReadOnlyList<KustoIngestion>> GetLatestIngestionsAsync()
        {
            var table = await GetKustoIngestionTableAsync();
            return await table
                .QueryAsync<KustoIngestion>(x => x.PartitionKey == KustoIngestion.DefaultPartitionKey)
                .Take(20)
                .ToListAsync();
        }

        public async Task<KustoIngestion> GetIngestionAsync(string rowKey)
        {
            var table = await GetKustoIngestionTableAsync();
            return await table.GetEntityOrNullAsync<KustoIngestion>(KustoIngestion.DefaultPartitionKey, rowKey);
        }

        public async Task AddIngestionAsync(KustoIngestion ingestion)
        {
            var table = await GetKustoIngestionTableAsync();
            await table.AddEntityAsync(ingestion);
        }

        public async Task<KustoContainerIngestion> GetContainerAsync(string storageSuffix, string containerName)
        {
            var table = await GetKustoIngestionTableAsync(storageSuffix);
            return await table.GetEntityOrNullAsync<KustoContainerIngestion>(KustoContainerIngestion.DefaultPartitionKey, containerName);
        }

        public async Task ReplaceIngestionAsync(KustoIngestion ingestion)
        {
            var table = await GetKustoIngestionTableAsync();
            var response = await table.UpdateEntityAsync(ingestion, ingestion.ETag, mode: TableUpdateMode.Replace);
            ingestion.UpdateETagAndTimestamp(response);
        }

        public async Task ReplaceContainerAsync(KustoContainerIngestion container)
        {
            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            var response = await table.UpdateEntityAsync(container, container.ETag, mode: TableUpdateMode.Replace);
            container.UpdateETagAndTimestamp(response);
        }

        public async Task DeleteContainerAsync(KustoContainerIngestion container)
        {
            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            await table.DeleteEntityAsync(container, container.ETag);
        }

        public async Task AddBlobsAsync(KustoContainerIngestion container, IReadOnlyList<KustoBlobIngestion> blobs)
        {
            var table = await GetKustoIngestionTableAsync(container.StorageSuffix);
            var existingBlobs = await GetBlobsAsync(table, container);

            var existingBuckets = existingBlobs.Select(x => x.Bucket).ToList();
            var missingBuckets = blobs.Select(x => x.Bucket).Except(existingBuckets).ToHashSet();
            var newBlobs = blobs.Where(x => missingBuckets.Contains(x.Bucket));

            var batch = new MutableTableTransactionalBatch(table);
            foreach (var blob in newBlobs)
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

        public async Task AddContainersAsync(KustoIngestion ingestion, IReadOnlyList<string> allContainerNames)
        {
            var table = await GetKustoIngestionTableAsync(ingestion.StorageSuffix);
            var containers = await GetContainersAsync(table, ingestion);

            var existingContainerNames = containers.Select(x => x.GetContainerName()).ToList();
            var missingContainerNames = allContainerNames.Except(existingContainerNames);
            if (existingContainerNames.Except(allContainerNames).Any())
            {
                throw new InvalidOperationException($"There are extra container names for ingestion '{ingestion.GetIngestionId()}'.");
            }

            if (missingContainerNames.Any())
            {
                var newContainers = missingContainerNames
                    .Select(x => new KustoContainerIngestion(x)
                    {
                        IngestionId = ingestion.GetIngestionId(),
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

        public async Task<IReadOnlyList<KustoContainerIngestion>> GetContainersAsync(KustoIngestion ingestion)
        {
            var table = await GetKustoIngestionTableAsync(ingestion.StorageSuffix);
            return await GetContainersAsync(table, ingestion);
        }

        private static async Task<List<KustoContainerIngestion>> GetContainersAsync(TableClient table, KustoIngestion ingestion)
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
