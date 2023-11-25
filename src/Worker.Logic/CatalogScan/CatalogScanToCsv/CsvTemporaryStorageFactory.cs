// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CsvTemporaryStorageFactory
    {
        private readonly AppendResultStorageService _storageService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<CsvTemporaryStorageFactory> _logger;

        public CsvTemporaryStorageFactory(
            AppendResultStorageService storageService,
            TaskStateStorageService taskStateStorageService,
            IMessageEnqueuer messageEnqueuer,
            IOptions<NuGetInsightsWorkerSettings> options,
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

        public IReadOnlyList<ICsvTemporaryStorage> Create<T1, T2, T3>(ICsvResultStorage<T1> storage1, ICsvResultStorage<T2> storage2, ICsvResultStorage<T3> storage3)
            where T1 : class, ICsvRecord
            where T2 : class, ICsvRecord
            where T3 : class, ICsvRecord
        {
            return new ICsvTemporaryStorage[]
            {
                new CatalogScanToCsvStorage<T1>(storage1.ResultContainerName, 0, this),
                new CatalogScanToCsvStorage<T2>(storage2.ResultContainerName, 1, this),
                new CatalogScanToCsvStorage<T3>(storage3.ResultContainerName, 2, this),
            };
        }

        public async Task InitializeAsync(string storageSuffix)
        {
            await _taskStateStorageService.InitializeAsync(storageSuffix);
        }

        public async Task FinalizeAsync(string storageSuffix)
        {
            await _taskStateStorageService.DeleteTableAsync(storageSuffix);
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

            public async Task InitializeAsync(string storageSuffix)
            {
                await _parent._storageService.InitializeAsync(GetTableName(storageSuffix), _resultsContainerName);
            }

            public async Task AppendAsync<TRecord>(string storageSuffix, IReadOnlyList<ICsvRecordSet<TRecord>> sets) where TRecord : class, ICsvRecord
            {
                await _parent._storageService.AppendAsync(
                    GetTableName(storageSuffix),
                    _parent._options.Value.AppendResultStorageBucketCount,
                    sets);
            }

            public async Task StartAggregateAsync(string aggregatePartitionKeyPrefix, string storageSuffix)
            {
                var buckets = await _parent._storageService.GetAppendedBucketsAsync(GetTableName(storageSuffix));

                var partitionKey = GetAggregateTasksPartitionKey(aggregatePartitionKeyPrefix);

                await _parent._taskStateStorageService.AddAsync(
                    storageSuffix,
                    partitionKey,
                    buckets.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList());

                var messages = buckets
                    .Select(b => new CsvCompactMessage<T>
                    {
                        SourceContainer = GetTableName(storageSuffix),
                        Bucket = b,
                        TaskStateKey = new TaskStateKey(
                            storageSuffix,
                            partitionKey,
                            b.ToString(CultureInfo.InvariantCulture)),
                    })
                    .ToList();
                await _parent._messageEnqueuer.EnqueueAsync(messages);
            }

            public async Task<bool> IsAggregateCompleteAsync(string aggregatePartitionKeyPrefix, string storageSuffix)
            {
                var countLowerBound = await _parent._taskStateStorageService.GetCountLowerBoundAsync(
                    storageSuffix,
                    GetAggregateTasksPartitionKey(aggregatePartitionKeyPrefix));
                _parent._logger.LogInformation("There are at least {Count} aggregate tasks pending.", countLowerBound);
                return countLowerBound == 0;
            }

            private string GetAggregateTasksPartitionKey(string aggregatePartitionKeyPrefix)
            {
                return $"{aggregatePartitionKeyPrefix}-{nameof(CatalogScanToCsvStorage<T>)}-aggregate-{_setIndex}";
            }

            public async Task FinalizeAsync(string storageSuffix)
            {
                await _parent._storageService.DeleteAsync(GetTableName(storageSuffix));
            }

            private string GetTableName(string suffix)
            {
                return $"{_parent._options.Value.CsvRecordTableName}{suffix}{_setIndex}";
            }
        }
    }
}
