using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Logic.Worker;
using Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using static Knapcode.ExplorePackages.Logic.Worker.TableStorageUtility;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly CatalogScanService _catalogScanService;
        private readonly IWorkerQueueFactory _workerQueueFactory;
        private readonly CursorStorageService _cursorStorageService;
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly LatestPackageLeafStorageService _latestPackageLeafStorageService;
        private readonly FindPackageAssetsStorageService _findPackageAssetsStorageService;
        private readonly ILogger<SandboxCommand> _logger;

        public SandboxCommand(
            ServiceClientFactory serviceClientFactory,
            CatalogScanService catalogScanService,
            IWorkerQueueFactory workerQueueFactory,
            CursorStorageService cursorStorageService,
            CatalogScanStorageService catalogScanStorageService,
            LatestPackageLeafStorageService latestPackageLeafStorageService,
            FindPackageAssetsStorageService findPackageAssetsStorageService,
            ILogger<SandboxCommand> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _catalogScanService = catalogScanService;
            _workerQueueFactory = workerQueueFactory;
            _cursorStorageService = cursorStorageService;
            _catalogScanStorageService = catalogScanStorageService;
            _latestPackageLeafStorageService = latestPackageLeafStorageService;
            _findPackageAssetsStorageService = findPackageAssetsStorageService;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _workerQueueFactory.GetQueue().CreateIfNotExistsAsync(retry: true);
            await _cursorStorageService.InitializeAsync();
            await _catalogScanStorageService.InitializeAsync();
            await _latestPackageLeafStorageService.InitializeAsync();
            await _findPackageAssetsStorageService.InitializeAsync();

            /*
            _logger.LogInformation("Clearing queues and tables...");
            await _serviceClientFactory.GetStorageAccount().CreateCloudQueueClient().GetQueueReference("queue").ClearAsync();
            await _serviceClientFactory.GetStorageAccount().CreateCloudQueueClient().GetQueueReference("queue-poison").ClearAsync();
            await _serviceClientFactory.GetStorageAccount().CreateCloudQueueClient().GetQueueReference("test").ClearAsync();
            await _serviceClientFactory.GetStorageAccount().CreateCloudQueueClient().GetQueueReference("test-poison").ClearAsync();
            await DeleteAllRowsAsync(_serviceClientFactory.GetStorageAccount().CreateCloudTableClient().GetTableReference("cursors"));
            await DeleteAllRowsAsync(_serviceClientFactory.GetLatestPackageLeavesStorageAccount().CreateCloudTableClient().GetTableReference("catalogindexscans"));
            await DeleteAllRowsAsync(_serviceClientFactory.GetLatestPackageLeavesStorageAccount().CreateCloudTableClient().GetTableReference("catalogpagescans"));
            await DeleteAllRowsAsync(_serviceClientFactory.GetLatestPackageLeavesStorageAccount().CreateCloudTableClient().GetTableReference("catalogleafscans"));
            await DeleteAllRowsAsync(_serviceClientFactory.GetLatestPackageLeavesStorageAccount().CreateCloudTableClient().GetTableReference("latestleaves"));
            */

            await _catalogScanService.UpdateAsync();
        }

        private async Task DeleteAllRowsAsync(CloudTable table)
        {
            TableContinuationToken token = null;
            do
            {
                var queryResult = await table.ExecuteQuerySegmentedAsync(new TableQuery(), token);
                token = queryResult.ContinuationToken;

                if (!queryResult.Results.Any())
                {
                    continue;
                }

                var partitionKeyGroups = new ConcurrentBag<IGrouping<string, DynamicTableEntity>>(queryResult.Results.GroupBy(x => x.PartitionKey));
                var maxKeyLength = partitionKeyGroups.Max(x => x.Key.Length);

                var workers = Enumerable
                    .Range(0, 32)
                    .Select(async x =>
                    {
                        while (partitionKeyGroups.TryTake(out var group))
                        {
                            var batch = new TableBatchOperation();
                            foreach (var row in group)
                            {
                                if (batch.Count >= MaxBatchSize)
                                {
                                    await ExecuteBatch(table, group.Key, batch);
                                    batch = new TableBatchOperation();
                                }

                                batch.Add(TableOperation.Delete(row));
                            }

                            if (batch.Count > 0)
                            {
                                await ExecuteBatch(table, group.Key.PadRight(maxKeyLength), batch);
                            }
                        }
                    })
                    .ToList();
                await Task.WhenAll(workers);
            }
            while (token != null);
        }

        private async Task ExecuteBatch(CloudTable table, string partitionKey, TableBatchOperation batch)
        {
            _logger.LogInformation("[ {TableName}, {PartitionKey} ] Deleting batch of {Count} rows...", table.Name, partitionKey, batch.Count);
            await table.ExecuteBatchAsync(batch);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => false;
        public bool IsSingleton() => false;

    }
}
