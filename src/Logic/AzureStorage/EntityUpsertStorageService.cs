// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

#nullable enable

namespace NuGet.Insights
{
    public enum EntityUpsertStrategy
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

    public record ItemWithEntityKey<TItem>(TItem Item, string PartitionKey, string RowKey)
    {
        public ItemWithEntityKey(TItem item, (string PartitionKey, string RowKey) key)
            : this(item, key.PartitionKey, key.RowKey)
        {
        }
    }

    public interface IEntityUpsertStorage<TItem, TEntity> where TEntity : class, ITableEntity, new()
    {
        Task<TEntity> MapAsync(string partitionKey, string rowKey, TItem item);
        (string PartitionKey, string RowKey) GetKey(TItem item);
        bool ShouldReplace(TItem item, TEntity entity);
        ItemWithEntityKey<TItem> GetItemFromRowKeyGroup(IGrouping<string, ItemWithEntityKey<TItem>> group);
        IReadOnlyList<string>? Select { get; }
        EntityUpsertStrategy Strategy { get; }
        TableClientWithRetryContext Table { get; }
    }

    public class EntityUpsertStorageService<TItem, TEntity> where TEntity : class, ITableEntity, new()
    {
        /// <summary>
        /// Range queries are 18 times the cost (https://azure.microsoft.com/en-us/pricing/details/storage/tables/)
        /// but a bunch of point reads can be slower.
        /// </summary>
        private const int PointReadThreshold = 10;

        public const string MetricIdPrefix = $"{nameof(EntityUpsertStorageService<TItem, TEntity>)}.";

        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<EntityUpsertStorageService<TItem, TEntity>> _logger;
        private readonly string _entityType;
        private readonly IMetric _addDurationMsCount;
        private readonly IMetric _addItemCount;
        private readonly IMetric _addOptimisticallyPartitionKeyCount;
        private readonly IMetric _addOptimisticallyRowKeyCount;
        private readonly IMetric _addOptimisticallyTryAgainCount;
        private readonly IMetric _addOptimisticallyTryAgainBatchRatio;
        private readonly IMetric _addOptimisticallyConflictCount;
        private readonly IMetric _addOptimisticallyUnknownCount;
        private readonly IMetric _addOptimisticallyUnknownBatchRatio;
        private readonly IMetric _addOptimisticallyBatchSize;
        private readonly IMetric _addOptimisticallyFailureIndex;
        private readonly IMetric _addOptimisticallyLoopCount;
        private readonly IMetric _pointUpdateMetric;
        private readonly IMetric _pointIgnoreMetric;
        private readonly IMetric _pointAddMetric;
        private readonly IMetric _rangeUpdateMetric;
        private readonly IMetric _rangeIgnoreMetric;
        private readonly IMetric _rangeAddMetric;
        private readonly IMetric _rangeWasteMetric;
        private readonly IMetric _rangeWasteRatioMetric;

        public EntityUpsertStorageService(
            ITelemetryClient telemetryClient,
            ILogger<EntityUpsertStorageService<TItem, TEntity>> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
            _entityType = typeof(TEntity).Name;

            _addDurationMsCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddAsync)}.DurationMs", "EntityType", "Strategy", "Success");
            _addItemCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddAsync)}.ItemCount", "EntityType", "Strategy");

            _addOptimisticallyPartitionKeyCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.PartitionKeyCount", "EntityType");
            _addOptimisticallyRowKeyCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.RowKeyCount", "EntityType");
            _addOptimisticallyTryAgainCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.TryAgainCount", "EntityType");
            _addOptimisticallyTryAgainBatchRatio = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.TryAgainBatchRatio", "EntityType");
            _addOptimisticallyConflictCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.ConflictCount", "EntityType");
            _addOptimisticallyUnknownCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.UnknownCount", "EntityType");
            _addOptimisticallyUnknownBatchRatio = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.UnknownBatchRatio", "EntityType");
            _addOptimisticallyBatchSize = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.BatchSize", "EntityType", "Success"); ;
            _addOptimisticallyFailureIndex = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.FailureIndex", "EntityType", "IsBatch", "StatusCode", "ErrorCode");
            _addOptimisticallyLoopCount = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(AddOptimisticallyAsync)}.LoopCount", "EntityType");

            _pointUpdateMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithPointReadAsync)}.UpdateCount", "EntityType");
            _pointIgnoreMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithPointReadAsync)}.IgnoreCount", "EntityType");
            _pointAddMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithPointReadAsync)}.AddCount", "EntityType");

            _rangeUpdateMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.UpdateCount", "EntityType");
            _rangeIgnoreMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.IgnoreCount", "EntityType");
            _rangeAddMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.AddCount", "EntityType");
            _rangeWasteMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.WasteCount", "EntityType");
            _rangeWasteRatioMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(SyncWithRangeQueryAsync)}.WasteRatio", "EntityType");
        }

        public async Task<List<TEntity>> AddAsync(
            IReadOnlyList<TItem> items,
            IEntityUpsertStorage<TItem, TEntity> storage)
        {
            var strategy = storage.Strategy.ToString();
            _addItemCount.TrackValue(items.Count, _entityType, strategy);
            var sw = Stopwatch.StartNew();
            var groups = GroupItems(items, storage);
            var entityCount = groups.Sum(g => g.Items.Count);
            List<TEntity> entities;

            try
            {
                switch (storage.Strategy)
                {
                    case EntityUpsertStrategy.ReadThenAdd:
                        entities = await ReadThenAddAsync(groups, storage);
                        break;
                    case EntityUpsertStrategy.AddOptimistically:
                        entities = await AddOptimisticallyAsync(groups, storage);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (entities.Count != entityCount)
                {
                    throw new InvalidOperationException($"The actual entity count ({entities.Count}) did not match the expected ({entityCount}).");
                }

                entities.Sort((a, b) =>
                {
                    var c = string.CompareOrdinal(a.PartitionKey, b.PartitionKey);
                    if (c != 0)
                    {
                        return c;
                    }

                    return string.CompareOrdinal(a.RowKey, b.RowKey);
                });

                _addDurationMsCount.TrackValue(sw.Elapsed.TotalMilliseconds, _entityType, strategy, "true");
            }
            catch
            {
                _addDurationMsCount.TrackValue(sw.Elapsed.TotalMilliseconds, _entityType, strategy, "false");
                throw;
            }

            return entities;
        }

        private static List<(string PartitionKey, List<ItemWithEntityKey<TItem>> Items)> GroupItems(
            IEnumerable<TItem> items,
            IEntityUpsertStorage<TItem, TEntity> storage)
        {
            return items
                .Select(x => new ItemWithEntityKey<TItem>(x, storage.GetKey(x)))
                .GroupBy(x => x.PartitionKey)
                .Select(g => (PartitionKey: g.Key, Items: g
                    .GroupBy(x => x.RowKey)
                    .Select(x => storage.GetItemFromRowKeyGroup(x))
                    .OrderBy(x => x.RowKey, StringComparer.Ordinal)
                    .ToList()))
                .ToList();
        }

        private async Task<List<TEntity>> AddOptimisticallyAsync(
            List<(string PartitionKey, List<ItemWithEntityKey<TItem>> Items)> groups,
            IEntityUpsertStorage<TItem, TEntity> storage)
        {
            var state = new OptimisticGroupState(storage, [], new MutableTableTransactionalBatch(storage.Table), [], [], [], [], [], [], []);
            _addOptimisticallyPartitionKeyCount.TrackValue(groups.Count, _entityType);

            foreach (var (partitionKey, group) in groups)
            {
                _addOptimisticallyRowKeyCount.TrackValue(group.Count, _entityType);
                state.Incoming.AddRange(group);
                var loopCount = 0;
                await AddOptimisticallyAsync(state, partitionKey);

                while (state.TryAgain.Count > 0 || state.Conflict.Count > 0 || state.Unknown.Count > 0)
                {
                    loopCount++;

                    _addOptimisticallyTryAgainCount.TrackValue(state.TryAgain.Count, _entityType);
                    _addOptimisticallyConflictCount.TrackValue(state.Conflict.Count, _entityType);
                    _addOptimisticallyUnknownCount.TrackValue(state.Unknown.Count, _entityType);

                    // Sync all of the known conflicts with point reads, add them to the next batch.
                    // Do this first, so we don't do too many point reads for conflicts.
                    if (state.Conflict.Count > 0)
                    {
                        var rowKeyToETag = await SyncWithPointReadAsync(partitionKey, state.Conflict, state.ResolvedEntities, storage);
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
                            var rowKeyToETag = await SyncWithPointReadAsync(partitionKey, state.Unknown, state.ResolvedEntities, storage);
                            UpdateRowKeyToETag(state, rowKeyToETag);
                        }
                        else
                        {
                            // otherwise fetch a page worth, so we can make at least some progress
                            var rowKeyToETag = await SyncWithRangeQueryAsync(partitionKey, state.Unknown, state.ResolvedEntities, storage, pageLimit: 1);
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

                _addOptimisticallyLoopCount.TrackValue(loopCount, _entityType);
                state.Incoming.Clear();
                state.Batch.Reset();
                state.RowKeyToEntity.Clear();
                state.TryAgain.Clear();
                state.Conflict.Clear();
                state.Unknown.Clear();
                state.RowKeyToETag.Clear();
            }

            return state.ResolvedEntities;
        }

        private async Task AddOptimisticallyAsync(OptimisticGroupState state, string partitionKey)
        {
            // first, add all of the entities that have etags
            var remaining = new List<ItemWithEntityKey<TItem>>();
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

                if (state.Batch.Count >= StorageUtility.MaxBatchSize)
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

                if (state.Batch.Count >= StorageUtility.MaxBatchSize)
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
            IEntityUpsertStorage<TItem, TEntity> Storage,
            List<ItemWithEntityKey<TItem>> Incoming,
            MutableTableTransactionalBatch Batch,
            List<ItemWithEntityKey<TItem>> BatchItems,
            Dictionary<string, TEntity> RowKeyToEntity,
            List<ItemWithEntityKey<TItem>> TryAgain,
            List<ItemWithEntityKey<TItem>> Conflict,
            List<ItemWithEntityKey<TItem>> Unknown,
            Dictionary<string, ETag> RowKeyToETag,
            List<TEntity> ResolvedEntities);

        private async Task SubmitBatchOptimisticallyAsync(OptimisticGroupState state, string partitionKey)
        {
            try
            {
                await state.Batch.SubmitBatchAsync();
                _addOptimisticallyBatchSize.TrackValue(state.BatchItems.Count, _entityType, "true");
                foreach (var itemWithKey in state.BatchItems)
                {
                    state.ResolvedEntities.Add(state.RowKeyToEntity[itemWithKey.RowKey]);
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
                AddConflictBatch(state, ex, ex.FailedTransactionActionIndex.Value);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict || ex.Status == (int)HttpStatusCode.PreconditionFailed)
            {
                AddConflictBatch(state, ex, failedIndex: 0);
            }
        }

        private void AddConflictBatch(OptimisticGroupState state, RequestFailedException ex, int failedIndex)
        {
            _addOptimisticallyBatchSize.TrackValue(state.BatchItems.Count, _entityType, "false");
            _addOptimisticallyTryAgainBatchRatio.TrackValue((1.0 * failedIndex) / state.BatchItems.Count, _entityType);
            _addOptimisticallyUnknownBatchRatio.TrackValue((1.0 * (state.BatchItems.Count - (failedIndex + 1))) / state.BatchItems.Count, _entityType);
            _addOptimisticallyFailureIndex.TrackValue(failedIndex, _entityType, state.Batch.Count > 1 ? "true" : "false", ex.Status.ToString(CultureInfo.InvariantCulture), ex.ErrorCode ?? "N/A");

            state.TryAgain.AddRange(state.BatchItems.Take(failedIndex));
            state.Conflict.Add(state.BatchItems[failedIndex]);
            state.Unknown.AddRange(state.BatchItems.Skip(failedIndex + 1));
            state.Batch.Reset();
            state.BatchItems.Clear();
        }

        private async Task<List<TEntity>> ReadThenAddAsync(
            List<(string PartitionKey, List<ItemWithEntityKey<TItem>> Items)> groups,
            IEntityUpsertStorage<TItem, TEntity> storage)
        {
            var entities = new List<TEntity>();

            foreach (var (partitionKey, group) in groups)
            {
                const int maxAttempts = 5;
                var attempt = 0;

                while (true)
                {
                    attempt++;
                    try
                    {
                        entities.AddRange(await AddAsync(partitionKey, group, storage));
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

            return entities;
        }

        private async Task<List<TEntity>> AddAsync(
            string partitionKey,
            List<ItemWithEntityKey<TItem>> itemsWithKeys,
            IEntityUpsertStorage<TItem, TEntity> storage)
        {
            var resolvedEntities = new List<TEntity>();

            Dictionary<string, ETag> rowKeyToETag;
            if (itemsWithKeys.Count <= PointReadThreshold)
            {
                rowKeyToETag = await SyncWithPointReadAsync(partitionKey, itemsWithKeys, resolvedEntities, storage);
            }
            else
            {
                rowKeyToETag = await SyncWithRangeQueryAsync(partitionKey, itemsWithKeys, resolvedEntities, storage, pageLimit: null);
            }

            // Update or insert the rows.
            var batch = new MutableTableTransactionalBatch(storage.Table);

            foreach (ItemWithEntityKey<TItem> itemWithKey in itemsWithKeys)
            {
                var entity = await storage.MapAsync(partitionKey, itemWithKey.RowKey, itemWithKey.Item);
                resolvedEntities.Add(entity);

                if (rowKeyToETag.TryGetValue(itemWithKey.RowKey, out var etag))
                {
                    entity.ETag = etag;
                    batch.UpdateEntity(entity, entity.ETag, mode: TableUpdateMode.Replace);
                }
                else
                {
                    batch.AddEntity(entity);
                }

                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                }
            }

            if (batch.Count > 0)
            {
                await batch.SubmitBatchAsync();
            }

            return resolvedEntities;
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
            List<ItemWithEntityKey<TItem>> itemsWithKeys,
            List<TEntity> resolvedEntities,
            IEntityUpsertStorage<TItem, TEntity> storage)
        {
            var rowKeyToETag = new Dictionary<string, ETag>();
            var itemsToKeep = new List<ItemWithEntityKey<TItem>>(itemsWithKeys.Count);

            var updateCount = 0;
            var ignoreCount = 0;
            var addCount = 0;

            foreach (var itemWithKey in itemsWithKeys)
            {
                var entity = await storage.Table.GetEntityOrNullAsync<TEntity>(partitionKey, itemWithKey.RowKey, storage.Select);
                if (entity is not null)
                {
                    if (!storage.ShouldReplace(itemWithKey.Item, entity))
                    {
                        // The version in Table Storage is newer, ignore the version we have.
                        ignoreCount++;
                        resolvedEntities.Add(entity);
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

            _pointUpdateMetric.TrackValue(updateCount, _entityType);
            _pointIgnoreMetric.TrackValue(ignoreCount, _entityType);
            _pointAddMetric.TrackValue(addCount, _entityType);

            itemsWithKeys.Clear();
            itemsWithKeys.AddRange(itemsToKeep);

            return rowKeyToETag;
        }

        private async Task<Dictionary<string, ETag>> SyncWithRangeQueryAsync(
            string partitionKey,
            List<ItemWithEntityKey<TItem>> itemsWithKeys,
            List<TEntity> resolvedEntities,
            IEntityUpsertStorage<TItem, TEntity> storage,
            int? pageLimit)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics(dimension1Name: "EntityType", _entityType);

            var rowKeyToItemWithKey = new Dictionary<string, ItemWithEntityKey<TItem>>();
            string? minRowKey = null;
            string? maxRowKey = null;
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
            Expression<Func<TEntity, bool>> filter = x =>
                x.PartitionKey == partitionKey
                && string.Compare(x.RowKey, minRowKey, StringComparison.Ordinal) >= 0
                && string.Compare(x.RowKey, maxRowKey, StringComparison.Ordinal) <= 0;

            var query = storage.Table
                .QueryAsync(
                    filter,
                    maxPerPage: StorageUtility.MaxTakeCount,
                    select: storage.Select)
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
                        if (!storage.ShouldReplace(itemWithKey.Item, result))
                        {
                            // The version in Table Storage is newer, ignore the version we have.
                            rowKeyToItemWithKey.Remove(result.RowKey);
                            resolvedEntities.Add(result);
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

            _rangeUpdateMetric.TrackValue(updateCount, _entityType);
            _rangeIgnoreMetric.TrackValue(ignoreCount, _entityType);
            _rangeWasteMetric.TrackValue(wasteCount, _entityType);
            _rangeAddMetric.TrackValue(rowKeyToItemWithKey.Count - rowKeyToEtag.Count, _entityType);

            var readRows = ignoreCount + updateCount + wasteCount;
            _rangeWasteRatioMetric.TrackValue(readRows > 0 ? (1.0 * wasteCount) / readRows : 0, _entityType);

            itemsWithKeys.Clear();
            itemsWithKeys.AddRange(rowKeyToItemWithKey.Values.OrderBy(x => x.RowKey, StringComparer.Ordinal));

            return rowKeyToEtag;
        }
    }
}
