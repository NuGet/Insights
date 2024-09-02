// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.Worker
{
    public enum LatestLeafStorageStrategy
    {
        /// <summary>
        /// Read the current latest leaves per partition key, then add/update items as needed.
        /// This is ideal for relatively small partitions. It is not ideal for large partitions because can read a
        /// large range of leaves between the min and max input row key. This is acceptable is the partition is package
        /// ID-based because few packages have many thousands of versions.
        /// </summary>
        ReadThenAdd,

        /// <summary>
        /// Aggressively adds the leaves before checking for existence. If a collision is found, the batch is split and
        /// a point read is done to determine which rows need to be updated.
        /// </summary>
        AddOptimistically,
    }

    public class LatestLeafStorageService<T> where T : class, ILatestPackageLeaf, new()
    {
        /// <summary>
        /// Range queries are 18 times the cost (https://azure.microsoft.com/en-us/pricing/details/storage/tables/)
        /// but a bunch of point reads can be slower.
        /// </summary>
        private const int PointReadThreshold = 10;

        public const string MetricIdPrefix = $"{nameof(LatestLeafStorageService<T>)}.";

        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<LatestLeafStorageService<T>> _logger;
        private readonly string _leafType;
        private readonly IMetric _addDurationMsCount;
        private readonly IMetric _addItemCount;
        private readonly IMetric _addOptimisticallyPartitionKeyCount;
        private readonly IMetric _addOptimisticallyRowKeyCount;
        private readonly IMetric _addOptimisticallyLoopCount;
        private readonly IMetric _addOptimisticallyTryAgainCount;
        private readonly IMetric _addOptimisticallyTryAgainBatchRatio;
        private readonly IMetric _addOptimisticallyConflictCount;
        private readonly IMetric _addOptimisticallyUnknownCount;
        private readonly IMetric _addOptimisticallyUnknownBatchRatio;
        private readonly IMetric _addOptimisticallyBatchSize;
        private readonly IMetric _pointUpdateMetric;
        private readonly IMetric _pointIgnoreMetric;
        private readonly IMetric _pointAddMetric;
        private readonly IMetric _rangeUpdateMetric;
        private readonly IMetric _rangeIgnoreMetric;
        private readonly IMetric _rangeAddMetric;
        private readonly IMetric _rangeWasteMetric;
        private readonly IMetric _rangeWasteRatioMetric;

        public LatestLeafStorageService(
            ITelemetryClient telemetryClient,
            ILogger<LatestLeafStorageService<T>> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
            _leafType = typeof(T).Name;

            _addDurationMsCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddAsync)}.DurationMs", "LeafType", "Strategy", "Success");
            _addItemCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddAsync)}.ItemCount", "LeafType", "Strategy");

            _addOptimisticallyPartitionKeyCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.PartitionKeyCount", "LeafType");
            _addOptimisticallyRowKeyCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.RowKeyCount", "LeafType");
            _addOptimisticallyTryAgainCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.TryAgainCount", "LeafType");
            _addOptimisticallyTryAgainBatchRatio = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.TryAgainBatchRatio", "LeafType");
            _addOptimisticallyConflictCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.ConflictCount", "LeafType");
            _addOptimisticallyUnknownCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.UnknownCount", "LeafType");
            _addOptimisticallyUnknownBatchRatio = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.UnknownBatchRatio", "LeafType");
            _addOptimisticallyBatchSize = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.BatchSize", "LeafType", "Success");

            _pointUpdateMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithPointReadAsync)}.UpdateCount", "LeafType");
            _pointIgnoreMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithPointReadAsync)}.IgnoreCount", "LeafType");
            _pointAddMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithPointReadAsync)}.AddCount", "LeafType");

            _rangeUpdateMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.UpdateCount", "LeafType");
            _rangeIgnoreMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.IgnoreCount", "LeafType");
            _rangeAddMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.AddCount", "LeafType");
            _rangeWasteMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.WasteCount", "LeafType");
            _rangeWasteRatioMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.WasteRatio", "LeafType");
        }

        public async Task AddAsync(
            IReadOnlyList<ICatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage)
        {
            var strategy = storage.Strategy.ToString();
            _addItemCount.TrackValue(items.Count, _leafType, strategy);
            var sw = Stopwatch.StartNew();

            try
            {
                switch (storage.Strategy)
                {
                    case LatestLeafStorageStrategy.ReadThenAdd:
                        await ReadThenAddAsync(items, storage);
                        break;
                    case LatestLeafStorageStrategy.AddOptimistically:
                        await AddOptimisticallyAsync(items, storage);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                _addDurationMsCount.TrackValue(sw.Elapsed.TotalMilliseconds, _leafType, strategy, "true");
            }
            catch
            {
                _addDurationMsCount.TrackValue(sw.Elapsed.TotalMilliseconds, _leafType, strategy, "false");
                throw;
            }
        }

        private static List<(string PartitionKey, List<ItemWithKey> Items)> GroupItems(
            IReadOnlyList<ICatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage)
        {
            return items
                .Select(x => new ItemWithKey(x, storage.GetKey(x)))
                .GroupBy(x => x.PartitionKey)
                .Select(g => (PartitionKey: g.Key, Items: g
                    .GroupBy(x => x.RowKey)
                    .Select(x => x.OrderByDescending(x => x.Item.CommitTimestamp).First())
                    .OrderBy(x => x.RowKey, StringComparer.Ordinal)
                    .ToList()))
                .ToList();
        }

        private async Task AddOptimisticallyAsync(IReadOnlyList<ICatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            var groups = GroupItems(items, storage);
            var state = new OptimisticGroupState(storage, [], new MutableTableTransactionalBatch(storage.Table), [], [], [], [], [], []);

            foreach (var (partitionKey, group) in groups)
            {
                _addOptimisticallyPartitionKeyCount.TrackValue(1, _leafType);
                _addOptimisticallyRowKeyCount.TrackValue(group.Count, _leafType);
                state.Incoming.AddRange(group);
                await AddOptimisticallyAsync(state, partitionKey);

                while (state.TryAgain.Count > 0 || state.Conflict.Count > 0 || state.Unknown.Count > 0)
                {
                    _addOptimisticallyTryAgainCount.TrackValue(state.TryAgain.Count, _leafType);
                    _addOptimisticallyConflictCount.TrackValue(state.Conflict.Count, _leafType);
                    _addOptimisticallyUnknownCount.TrackValue(state.Unknown.Count, _leafType);

                    // Sync all of the known conflicts with point reads, add them to the next batch.
                    // Do this first, so we don't do too many point reads for conflicts.
                    if (state.Conflict.Count > 0)
                    {
                        var rowKeyToETag = await SyncWithPointReadAsync(partitionKey, state.Conflict, state.Storage);
                        UpdateRowKeyToETag(state, rowKeyToETag);
                        state.TryAgain.AddRange(state.Conflict);
                        state.Conflict.Clear();
                    }

                    // Process the unknown entities incrementally, not processing too much at one time.
                    // Do this before another add call, to avoid wasting extra records appearing in a range query.
                    if (state.Unknown.Count > 0)
                    {
                        if (state.Unknown.Count <= PointReadThreshold)
                        {
                            // if there are only a few unknown entities left, get their etags with point reads, remove entities that are not the latest
                            var rowKeyToETag = await SyncWithPointReadAsync(partitionKey, state.Unknown, state.Storage);
                            UpdateRowKeyToETag(state, rowKeyToETag);
                        }
                        else
                        {
                            // otherwise fetch a page worth, so we can make at least some progress
                            var rowKeyToETag = await SyncWithRangeQueryAsync(partitionKey, state.Unknown, storage, pageLimit: 1);
                            UpdateRowKeyToETag(state, rowKeyToETag);
                        }

                        state.Incoming.AddRange(state.Unknown);
                        state.Unknown.Clear();
                        await AddOptimisticallyAsync(state, partitionKey);
                    }

                    // Update the synced conflicts and the entities that succeeded but were rolled back
                    if (state.TryAgain.Count > 0)
                    {
                        state.Incoming.AddRange(state.TryAgain);
                        state.TryAgain.Clear();
                        await AddOptimisticallyAsync(state, partitionKey);
                    }
                }

                state.Incoming.Clear();
                state.Batch.Reset();
                state.RowKeyToEntity.Clear();
                state.TryAgain.Clear();
                state.Conflict.Clear();
                state.Unknown.Clear();
                state.RowKeyToETag.Clear();
            }
        }

        private async Task AddOptimisticallyAsync(OptimisticGroupState state, string partitionKey)
        {
            // first, add all of the entities that have etags
            var remaining = new List<ItemWithKey>();
            foreach (var itemWithKey in state.Incoming)
            {
                if (!state.RowKeyToEntity.TryGetValue(itemWithKey.RowKey, out var entity))
                {
                    entity = await state.Storage.MapAsync(partitionKey, itemWithKey.RowKey, itemWithKey.Item);
                    state.RowKeyToEntity[itemWithKey.RowKey] = entity;
                }

                if (state.RowKeyToETag.TryGetValue(itemWithKey.RowKey, out var etag))
                {
                    entity.ETag = etag;
                    state.Batch.UpdateEntity(entity, entity.ETag, mode: TableUpdateMode.Replace);
                    state.BatchItems.Add(itemWithKey);
                }
                else
                {
                    remaining.Add(itemWithKey);
                }

                if (state.Batch.Count >= MaxBatchSize)
                {
                    await SubmitBatchOptimisticallyAsync(state, partitionKey);
                }
            }

            if (state.Batch.Count > 0)
            {
                await SubmitBatchOptimisticallyAsync(state, partitionKey);
            }

            // next, add all of the entities that do not have etags
            foreach (var itemWithKey in remaining)
            {
                state.Batch.AddEntity(state.RowKeyToEntity[itemWithKey.RowKey]);
                state.BatchItems.Add(itemWithKey);

                if (state.Batch.Count >= MaxBatchSize)
                {
                    await SubmitBatchOptimisticallyAsync(state, partitionKey);
                }
            }

            if (state.Batch.Count > 0)
            {
                await SubmitBatchOptimisticallyAsync(state, partitionKey);
            }

            state.Incoming.Clear();
        }

        private record OptimisticGroupState(
            ILatestPackageLeafStorage<T> Storage,
            List<ItemWithKey> Incoming,
            MutableTableTransactionalBatch Batch,
            List<ItemWithKey> BatchItems,
            Dictionary<string, T> RowKeyToEntity,
            List<ItemWithKey> TryAgain,
            List<ItemWithKey> Conflict,
            List<ItemWithKey> Unknown,
            Dictionary<string, ETag> RowKeyToETag);

        private async Task SubmitBatchOptimisticallyAsync(OptimisticGroupState state, string partitionKey)
        {
            try
            {
                await state.Batch.SubmitBatchAsync();
                _addOptimisticallyBatchSize.TrackValue(state.BatchItems.Count, _leafType, "true");
                foreach (var itemWithKey in state.BatchItems)
                {
                    state.RowKeyToEntity.Remove(itemWithKey.RowKey);
                    state.RowKeyToETag.Remove(itemWithKey.RowKey);
                }
                state.BatchItems.Clear();
            }
            catch (TableTransactionFailedException ex) when (
                ex.FailedTransactionActionIndex.HasValue
                && (ex.Status == (int)HttpStatusCode.Conflict
                    || ex.Status == (int)HttpStatusCode.PreconditionFailed))
            {
                _addOptimisticallyBatchSize.TrackValue(state.BatchItems.Count, _leafType, "false");
                var failedIndex = ex.FailedTransactionActionIndex.Value;

                _logger.LogTransientWarning(
                    ex,
                    "Submitting {Count} entities for partition key {PartitionKey} in a transaction failed due to an HTTP {StatusCode} on index {FailedIndex}.",
                    state.Batch.Count,
                    partitionKey,
                    ex.Status,
                    failedIndex);

                AddConflictBatch(state, failedIndex);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict || ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                _addOptimisticallyBatchSize.TrackValue(state.BatchItems.Count, _leafType, "false");
                _logger.LogTransientWarning(
                    ex,
                    "Submitting {Count} entities for partition key {PartitionKey} failed due to an HTTP {StatusCode}.",
                    state.Batch.Count,
                    partitionKey,
                    ex.Status);

                AddConflictBatch(state, failedIndex: 0);
            }
        }

        private void AddConflictBatch(OptimisticGroupState state, int failedIndex)
        {
            _addOptimisticallyTryAgainBatchRatio.TrackValue((1.0 * failedIndex) / state.BatchItems.Count, _leafType);
            _addOptimisticallyUnknownBatchRatio.TrackValue((1.0 * (state.BatchItems.Count - (failedIndex + 1))) / state.BatchItems.Count, _leafType);

            state.TryAgain.AddRange(state.BatchItems.Take(failedIndex));
            state.Conflict.Add(state.BatchItems[failedIndex]);
            state.Unknown.AddRange(state.BatchItems.Skip(failedIndex + 1));
            state.Batch.Reset();
            state.BatchItems.Clear();
        }

        private async Task ReadThenAddAsync(IReadOnlyList<ICatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            var groups = GroupItems(items, storage);

            foreach (var (partitionKey, group) in groups)
            {
                const int maxAttempts = 5;
                var attempt = 0;

                while (true)
                {
                    attempt++;
                    try
                    {
                        await AddAsync(partitionKey, group, storage);
                        break;
                    }
                    catch (RequestFailedException ex) when (attempt < maxAttempts
                        && (ex.Status == (int)HttpStatusCode.Conflict
                            || ex.Status == (int)HttpStatusCode.PreconditionFailed))
                    {
                        _logger.LogTransientWarning(
                            ex,
                            "Attempt {Attempt}: adding entities for partition key {PartitionKey} failed due to an HTTP {StatusCode}. Trying again.",
                            attempt,
                            partitionKey,
                            ex.Status);
                    }
                }
            }
        }

        private async Task AddAsync(
            string partitionKey,
            List<ItemWithKey> itemsWithKeys,
            ILatestPackageLeafStorage<T> storage)
        {
            Dictionary<string, ETag> rowKeyToETag;
            if (itemsWithKeys.Count <= PointReadThreshold)
            {
                rowKeyToETag = await SyncWithPointReadAsync(partitionKey, itemsWithKeys, storage);
            }
            else
            {
                rowKeyToETag = await SyncWithRangeQueryAsync(partitionKey, itemsWithKeys, storage, pageLimit: null);
            }

            // Update or insert the rows.
            var batch = new MutableTableTransactionalBatch(storage.Table);

            foreach (ItemWithKey itemWithKey in itemsWithKeys)
            {
                var entity = await storage.MapAsync(partitionKey, itemWithKey.RowKey, itemWithKey.Item);

                if (rowKeyToETag.TryGetValue(itemWithKey.RowKey, out var etag))
                {
                    entity.ETag = etag;
                    batch.UpdateEntity(entity, entity.ETag, mode: TableUpdateMode.Replace);
                }
                else
                {
                    batch.AddEntity(entity);
                }

                if (batch.Count >= MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                }
            }

            if (batch.Count > 0)
            {
                await batch.SubmitBatchAsync();
            }
        }

        private static void UpdateRowKeyToETag(OptimisticGroupState state, Dictionary<string, ETag> rowKeyToETag)
        {
            foreach (var (rowKey, etag) in rowKeyToETag)
            {
                state.RowKeyToETag[rowKey] = etag;
            }
        }

        private async Task<Dictionary<string, ETag>> SyncWithPointReadAsync(
            string partitionKey,
            List<ItemWithKey> itemsWithKeys,
            ILatestPackageLeafStorage<T> storage)
        {
            var rowKeyToETag = new Dictionary<string, ETag>();
            var itemsToKeep = new List<ItemWithKey>(itemsWithKeys.Count);

            var updateCount = 0;
            var ignoreCount = 0;
            var addCount = 0;

            foreach (var itemWithKey in itemsWithKeys)
            {
                var entity = await storage.Table.GetEntityOrNullAsync<T>(partitionKey, itemWithKey.RowKey);
                if (entity is not null)
                {
                    if (entity.CommitTimestamp >= itemWithKey.Item.CommitTimestamp)
                    {
                        // The version in Table Storage is newer, ignore the version we have.
                        ignoreCount++;
                        continue;
                    }

                    rowKeyToETag.Add(itemWithKey.RowKey, entity.ETag);
                    updateCount++;
                }
                else
                {
                    addCount++;
                }

                itemsToKeep.Add(itemWithKey);
            }

            _pointUpdateMetric.TrackValue(updateCount, _leafType);
            _pointIgnoreMetric.TrackValue(ignoreCount, _leafType);
            _pointAddMetric.TrackValue(addCount, _leafType);

            itemsWithKeys.Clear();
            itemsWithKeys.AddRange(itemsToKeep);

            return rowKeyToETag;
        }

        private async Task<Dictionary<string, ETag>> SyncWithRangeQueryAsync(
            string partitionKey,
            List<ItemWithKey> itemsWithKeys,
            ILatestPackageLeafStorage<T> storage,
            int? pageLimit)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics("LeafType", _leafType);

            var rowKeyToItemWithKey = new Dictionary<string, ItemWithKey>();
            string minRowKey = null;
            string maxRowKey = null;
            foreach (var itemWithKey in itemsWithKeys)
            {
                rowKeyToItemWithKey.Add(itemWithKey.RowKey, itemWithKey);

                if (minRowKey == null || string.CompareOrdinal(itemWithKey.RowKey, minRowKey) < 0)
                {
                    minRowKey = itemWithKey.RowKey;
                }

                if (maxRowKey == null || string.CompareOrdinal(itemWithKey.RowKey, maxRowKey) > 0)
                {
                    maxRowKey = itemWithKey.RowKey;
                }
            }

            var rowKeyToEtag = new Dictionary<string, ETag>();

            // Query for all of the version data in Table Storage, determining what needs to be updated.
            Expression<Func<T, bool>> filter = x =>
                x.PartitionKey == partitionKey
                && x.RowKey.CompareTo(minRowKey) >= 0
                && x.RowKey.CompareTo(maxRowKey) <= 0;

            var query = storage.Table
                .QueryAsync(
                    filter,
                    maxPerPage: MaxTakeCount,
                    select: [RowKey, storage.CommitTimestampColumnName])
                .AsPages();

            var updateCount = 0;
            var ignoreCount = 0;
            var wasteCount = 0;
            var pageCount = 0;
            await using var enumerator = query.GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(metrics))
            {
                pageCount++;

                foreach (var result in enumerator.Current.Values)
                {
                    if (rowKeyToItemWithKey.TryGetValue(result.RowKey, out var itemWithKey))
                    {
                        if (result.CommitTimestamp >= itemWithKey.Item.CommitTimestamp)
                        {
                            // The version in Table Storage is newer, ignore the version we have.
                            rowKeyToItemWithKey.Remove(result.RowKey);
                            ignoreCount++;
                        }
                        else
                        {
                            // The version in Table Storage is older, save the etag to update it.
                            rowKeyToEtag.Add(result.RowKey, result.ETag);
                            updateCount++;
                        }
                    }
                    else
                    {
                        wasteCount++;
                    }
                }

                if (pageLimit.HasValue && pageCount >= pageLimit.Value)
                {
                    break;
                }
            }

            _rangeUpdateMetric.TrackValue(updateCount, _leafType);
            _rangeIgnoreMetric.TrackValue(ignoreCount, _leafType);
            _rangeWasteMetric.TrackValue(wasteCount, _leafType);
            _rangeAddMetric.TrackValue(rowKeyToItemWithKey.Count - rowKeyToEtag.Count, _leafType);

            var readRows = ignoreCount + updateCount + wasteCount;
            _rangeWasteRatioMetric.TrackValue(readRows > 0 ? (1.0 * wasteCount) / readRows : 0, _leafType);

            itemsWithKeys.Clear();
            itemsWithKeys.AddRange(rowKeyToItemWithKey.Values.OrderBy(x => x.RowKey, StringComparer.Ordinal));

            return rowKeyToEtag;
        }

        private record ItemWithKey(ICatalogLeafItem Item, string PartitionKey, string RowKey)
        {
            public ItemWithKey(ICatalogLeafItem item, (string PartitionKey, string RowKey) key)
                : this(item, key.PartitionKey, key.RowKey)
            {
            }
        }
    }
}
