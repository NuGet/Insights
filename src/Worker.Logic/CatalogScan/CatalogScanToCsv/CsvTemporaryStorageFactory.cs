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

        public IReadOnlyList<ICsvTemporaryStorage> Create<T>(ICsvResultStorage<T> storage) where T : class, IAggregatedCsvRecord<T>
        {
            return
            [
                new CatalogScanToCsvStorage<T>(0, this)
            ];
        }

        public IReadOnlyList<ICsvTemporaryStorage> Create<T1, T2>(ICsvResultStorage<T1> storage1, ICsvResultStorage<T2> storage2)
            where T1 : class, IAggregatedCsvRecord<T1>
            where T2 : class, IAggregatedCsvRecord<T2>
        {
            return
            [
                new CatalogScanToCsvStorage<T1>(0, this),
                new CatalogScanToCsvStorage<T2>(1, this),
            ];
        }

        public IReadOnlyList<ICsvTemporaryStorage> Create<T1, T2, T3>(ICsvResultStorage<T1> storage1, ICsvResultStorage<T2> storage2, ICsvResultStorage<T3> storage3)
            where T1 : class, IAggregatedCsvRecord<T1>
            where T2 : class, IAggregatedCsvRecord<T2>
            where T3 : class, IAggregatedCsvRecord<T3>
        {
            return
            [
                new CatalogScanToCsvStorage<T1>(0, this),
                new CatalogScanToCsvStorage<T2>(1, this),
                new CatalogScanToCsvStorage<T3>(2, this),
            ];
        }

        public async Task InitializeAsync(string storageSuffix)
        {
            await _taskStateStorageService.InitializeAsync(storageSuffix);
        }

        public async Task FinalizeAsync(string storageSuffix)
        {
            await _taskStateStorageService.DeleteTableAsync(storageSuffix);
        }

        private class CatalogScanToCsvStorage<T> : ICsvTemporaryStorage where T : class, IAggregatedCsvRecord<T>
        {
            private readonly int _setIndex;
            private readonly CsvTemporaryStorageFactory _parent;

            public CatalogScanToCsvStorage(
                int setIndex,
                CsvTemporaryStorageFactory parent)
            {
                _setIndex = setIndex;
                _parent = parent;
            }

            public async Task InitializeAsync(string storageSuffix)
            {
                await _parent._storageService.InitializeAsync(GetTableName(storageSuffix));
            }

            public async Task AppendAsync<TRecord>(string storageSuffix, IReadOnlyList<TRecord> records) where TRecord : class, IAggregatedCsvRecord
            {
                var typedRecords = new List<T>(records.Count);
                foreach (var record in records)
                {
                    // validate type
                    if (record is not T typedRecord)
                    {
                        throw new InvalidOperationException($"A record was found with the wrong type. Expected: {typeof(T).FullName}. Actual: {record.GetType().FullName}.");
                    }

                    typedRecords.Add(typedRecord);
                }

                await _parent._storageService.AppendAsync(
                    GetTableName(storageSuffix),
                    _parent._options.Value.AppendResultStorageBucketCount,
                    typedRecords);
            }

            public async Task StartAggregateAsync(string aggregatePartitionKeyPrefix, string storageSuffix)
            {
                var buckets = await _parent._storageService.GetAppendedBucketsAsync(GetTableName(storageSuffix));

                var partitionKey = GetAggregateTasksPartitionKey(aggregatePartitionKeyPrefix);

                var messages = buckets
                    .Select(b => new CsvCompactMessage<T>
                    {
                        SourceTable = GetTableName(storageSuffix),
                        Bucket = b,
                        TaskStateKey = new TaskStateKey(
                            storageSuffix,
                            partitionKey,
                            b.ToString(CultureInfo.InvariantCulture)),
                    })
                    .ToList();
                await _parent._messageEnqueuer.EnqueueAsync(messages);

                await _parent._taskStateStorageService.GetOrAddAsync(
                    storageSuffix,
                    partitionKey,
                    buckets.Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList());
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
                return $"{_parent._options.Value.CsvRecordTableNamePrefix}{suffix}{_setIndex}";
            }
        }
    }
}
