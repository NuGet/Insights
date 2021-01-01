using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogScanToCsvAdapter<T> where T : ICsvRecord<T>, new()
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CatalogScanToCsvAdapter<T>> _logger;

        public CatalogScanToCsvAdapter(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            MessageEnqueuer messageEnqueuer,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<CatalogScanToCsvAdapter<T>> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _messageEnqueuer = messageEnqueuer;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan, string resultsContainerName)
        {
            await _storageService.InitializeAsync(GetTableName(indexScan.StorageSuffix), resultsContainerName);
            await _taskStateStorageService.InitializeAsync(indexScan.StorageSuffix);
        }

        public async Task AppendAsync(string storageSuffix, int bucketCount, string bucketKey, IReadOnlyList<T> records)
        {
            await _storageService.AppendAsync(GetTableName(storageSuffix), bucketCount, bucketKey, records);
        }

        public async Task StartAggregateAsync(CatalogIndexScan indexScan)
        {
            var buckets = await _storageService.GetWrittenBucketsAsync(GetTableName(indexScan.StorageSuffix));

            var partitionKey = GetAggregateTasksPartitionKey(indexScan);

            await _taskStateStorageService.AddAllAsync(
                indexScan.StorageSuffix,
                partitionKey,
                buckets.Select(x => x.ToString()).ToList());

            var messages = buckets
                .Select(b => new CsvCompactMessage<T>
                {
                    SourceContainer = GetTableName(indexScan.StorageSuffix),
                    Bucket = b,
                    TaskStateKey = new TaskStateKey(
                        indexScan.StorageSuffix,
                        partitionKey,
                         b.ToString()),
                })
                .ToList();
            await _messageEnqueuer.EnqueueAsync(messages);
        }

        public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
        {
            var countLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(
                indexScan.StorageSuffix,
                GetAggregateTasksPartitionKey(indexScan));
            _logger.LogInformation("There are at least {Count} compact tasks pending.", countLowerBound);
            return countLowerBound == 0;
        }

        private static string GetAggregateTasksPartitionKey(CatalogIndexScan indexScan)
        {
            return $"{indexScan.ScanId}-aggregate";
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            if (!string.IsNullOrEmpty(indexScan.StorageSuffix))
            {
                await _taskStateStorageService.DeleteTableAsync(indexScan.StorageSuffix);
                await _storageService.DeleteAsync(GetTableName(indexScan.StorageSuffix));
            }
        }

        private string GetTableName(string suffix)
        {
            return $"{_options.Value.CsvRecordsTableName}{suffix}";
        }
    }
}
