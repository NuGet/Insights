using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker
{
    public class CsvTemporaryStorageFactory
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<CsvTemporaryStorageFactory> _logger;

        public CsvTemporaryStorageFactory(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            IMessageEnqueuer messageEnqueuer,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<CsvTemporaryStorageFactory> logger)
        {
            _storageService = storageService;
            _taskStateStorageService = taskStateStorageService;
            _messageEnqueuer = messageEnqueuer;
            _options = options;
            _logger = logger;
        }

        public IReadOnlyList<ICsvTemporaryStorage> Create<T>(ICsvResultStorage<T> storage) where T : class, ICsvRecord
        {
            return new[]
            {
                new CatalogScanToCsvStorage<T>(storage.ResultContainerName, 0, this)
            };
        }

        public IReadOnlyList<ICsvTemporaryStorage> Create<T1, T2>(ICsvResultStorage<T1> storage1, ICsvResultStorage<T2> storage2)
            where T1 : class, ICsvRecord
            where T2 : class, ICsvRecord
        {
            return new ICsvTemporaryStorage[]
            {
                new CatalogScanToCsvStorage<T1>(storage1.ResultContainerName, 0, this),
                new CatalogScanToCsvStorage<T2>(storage2.ResultContainerName, 1, this),
            };
        }

        public async Task InitializeAsync(CatalogIndexScan indexScan)
        {
            await _taskStateStorageService.InitializeAsync(indexScan.StorageSuffix);
        }

        public async Task FinalizeAsync(CatalogIndexScan indexScan)
        {
            await _taskStateStorageService.DeleteTableAsync(indexScan.StorageSuffix);
        }

        private class CatalogScanToCsvStorage<T> : ICsvTemporaryStorage where T : class, ICsvRecord
        {
            private readonly string _resultsContainerName;
            private readonly int _setIndex;
            private readonly CsvTemporaryStorageFactory _parent;

            public CatalogScanToCsvStorage(
                string resultsContainerName,
                int setIndex,
                CsvTemporaryStorageFactory parent)
            {
                _resultsContainerName = resultsContainerName;
                _setIndex = setIndex;
                _parent = parent;

            }

            public async Task InitializeAsync(CatalogIndexScan indexScan)
            {
                await _parent._storageService.InitializeAsync(GetTableName(indexScan.StorageSuffix), _resultsContainerName);
            }

            public async Task StartCustomExpandAsync(CatalogIndexScan indexScan)
            {
                var buckets = await _parent._storageService.GetCompactedBucketsAsync(_resultsContainerName);

                var partitionKey = GetCustomExpandPartitionKey(indexScan);

                await _parent._taskStateStorageService.AddAsync(
                    indexScan.StorageSuffix,
                    partitionKey,
                    buckets.Select(x => x.ToString()).ToList());

                var messages = buckets
                    .Select(b => new CsvExpandReprocessMessage<T>
                    {
                        CursorName = indexScan.GetCursorName(),
                        ScanId = indexScan.GetScanId(),
                        Bucket = b,
                        TaskStateKey = new TaskStateKey(
                            indexScan.StorageSuffix,
                            partitionKey,
                            b.ToString()),
                    })
                    .ToList();
                await _parent._messageEnqueuer.EnqueueAsync(messages);
            }

            public async Task<bool> IsCustomExpandCompleteAsync(CatalogIndexScan indexScan)
            {
                var countLowerBound = await _parent._taskStateStorageService.GetCountLowerBoundAsync(
                    indexScan.StorageSuffix,
                    GetCustomExpandPartitionKey(indexScan));
                _parent._logger.LogInformation("There are at least {Count} expand custom tasks pending.", countLowerBound);
                return countLowerBound == 0;
            }

            private string GetCustomExpandPartitionKey(CatalogIndexScan indexScan)
            {
                return $"{indexScan.GetScanId()}-{nameof(CatalogScanToCsvStorage<T>)}-custom-expand-{_setIndex}";
            }

            public async Task AppendAsync<TRecord>(string storageSuffix, CsvRecordSet<TRecord> set) where TRecord : class, ICsvRecord
            {
                if (set.Records.Any())
                {
                    await _parent._storageService.AppendAsync(
                        GetTableName(storageSuffix),
                        _parent._options.Value.AppendResultStorageBucketCount,
                        set.BucketKey,
                        set.Records);
                }
            }

            public async Task StartAggregateAsync(CatalogIndexScan indexScan)
            {
                var buckets = await _parent._storageService.GetAppendedBucketsAsync(GetTableName(indexScan.StorageSuffix));

                var partitionKey = GetAggregateTasksPartitionKey(indexScan);

                await _parent._taskStateStorageService.AddAsync(
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
                await _parent._messageEnqueuer.EnqueueAsync(messages);
            }

            public async Task<bool> IsAggregateCompleteAsync(CatalogIndexScan indexScan)
            {
                var countLowerBound = await _parent._taskStateStorageService.GetCountLowerBoundAsync(
                    indexScan.StorageSuffix,
                    GetAggregateTasksPartitionKey(indexScan));
                _parent._logger.LogInformation("There are at least {Count} aggregate tasks pending.", countLowerBound);
                return countLowerBound == 0;
            }

            private string GetAggregateTasksPartitionKey(CatalogIndexScan indexScan)
            {
                return $"{indexScan.GetScanId()}-{nameof(CatalogScanToCsvStorage<T>)}-aggregate-{_setIndex}";
            }

            public async Task FinalizeAsync(CatalogIndexScan indexScan)
            {
                await _parent._storageService.DeleteAsync(GetTableName(indexScan.StorageSuffix));
            }

            private string GetTableName(string suffix)
            {
                return $"{_parent._options.Value.CsvRecordTableName}{suffix}{_setIndex}";
            }
        }
    }
}
