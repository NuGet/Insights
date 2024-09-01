// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.Worker
{
    public class LatestLeafStorageService<T> where T : class, ILatestPackageLeaf, new()
    {
        public const string MetricIdPrefix = $"{nameof(LatestLeafStorageService<T>)}.";

        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<LatestLeafStorageService<T>> _logger;
        private readonly string _leafType;
        private readonly IMetric _updateMetric;
        private readonly IMetric _ignoreMetric;
        private readonly IMetric _addMetric;
        private readonly IMetric _wasteMetric;
        private readonly IMetric _wasteRatioMetric;

        public LatestLeafStorageService(
            ITelemetryClient telemetryClient,
            ILogger<LatestLeafStorageService<T>> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
            _leafType = typeof(T).Name;
            _updateMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(GetExistingsRowsAsync)}.UpdateCount", "LeafType");
            _ignoreMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(GetExistingsRowsAsync)}.IgnoreCount", "LeafType");
            _addMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(GetExistingsRowsAsync)}.AddCount", "LeafType");
            _wasteMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(GetExistingsRowsAsync)}.WasteCount", "LeafType");
            _wasteRatioMetric = telemetryClient.GetMetric($"{MetricIdPrefix}{nameof(GetExistingsRowsAsync)}.WasteRatio", "LeafType");
        }

        public async Task AddAsync(
            IReadOnlyList<ICatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage,
            bool allowRetries)
        {
            var itemsWithKeys = items.Select(x => new ItemWithKey(x, storage.GetKey(x))).ToList();

            foreach (var group in itemsWithKeys.GroupBy(x => x.PartitionKey))
            {
                var maxAttempts = allowRetries ? 5 : 1;
                var attempt = 0;
                var groupList = group.ToList();

                while (true)
                {
                    attempt++;
                    try
                    {
                        await AddAsync(group.Key, groupList, storage);
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
                            group.Key,
                            ex.Status);
                    }
                }
            }
        }

        private async Task AddAsync(string partitionKey, IReadOnlyList<ItemWithKey> itemsWithKeys, ILatestPackageLeafStorage<T> storage)
        {
            (var rowKeyToItem, var rowKeyToETag) = await GetExistingsRowsAsync(partitionKey, itemsWithKeys, storage);

            // Add the row keys that remain. These are the versions that are not in Table Storage or are newer than the
            // commit timestamp in Table Storage.
            var rowKeysToUpsert = new List<string>();
            rowKeysToUpsert.AddRange(rowKeyToItem.Keys);
            rowKeysToUpsert.Sort(StringComparer.Ordinal);

            // Update or insert the rows.
            var batch = new MutableTableTransactionalBatch(storage.Table);
            T lastEntity = null;
            for (var i = 0; i < rowKeysToUpsert.Count; i++)
            {
                if (batch.Count >= MaxBatchSize)
                {
                    await ExecuteBatchAsync(batch, lastEntity);
                    batch = new MutableTableTransactionalBatch(storage.Table);
                }

                var rowKey = rowKeysToUpsert[i];
                var leaf = rowKeyToItem[rowKey];
                var entity = await storage.MapAsync(partitionKey, rowKey, leaf);
                lastEntity = entity;

                if (rowKeyToETag.TryGetValue(rowKey, out var etag))
                {
                    entity.ETag = etag;
                    batch.UpdateEntity(entity, entity.ETag, mode: TableUpdateMode.Replace);
                }
                else
                {
                    batch.AddEntity(entity);
                }
            }

            if (batch.Count > 0)
            {
                await ExecuteBatchAsync(batch, lastEntity);
            }
        }

        private async Task<(Dictionary<string, ICatalogLeafItem> RowKeyToItem, Dictionary<string, ETag> RowKeyToETag)> GetExistingsRowsAsync(
            string partitionKey,
            IEnumerable<ItemWithKey> itemsWithKeys,
            ILatestPackageLeafStorage<T> storage)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics("LeafType", _leafType);

            // Sort items by lexicographical order, since this is what table storage does.
            var itemList = itemsWithKeys
                .GroupBy(x => x.RowKey)
                .Select(x => x.OrderByDescending(x => x.Item.CommitTimestamp).First())
                .OrderBy(x => x.RowKey, StringComparer.Ordinal)
                .Select(x => (x.Item, x.RowKey))
                .ToList();
            var rowKeyToItem = itemList.ToDictionary(x => x.RowKey, x => x.Item);
            var rowKeyToEtag = new Dictionary<string, ETag>();

            // Query for all of the version data in Table Storage, determining what needs to be updated.
            Expression<Func<T, bool>> filter = x =>
                x.PartitionKey == partitionKey
                && x.RowKey.CompareTo(itemList.First().RowKey) >= 0
                && x.RowKey.CompareTo(itemList.Last().RowKey) <= 0;

            var query = storage.Table
                .QueryAsync(
                    filter,
                    maxPerPage: MaxTakeCount,
                    select: [RowKey, storage.CommitTimestampColumnName])
                .AsPages();

            var updateCount = 0;
            var ignoreCount = 0;
            var wasteCount = 0;
            await using var enumerator = query.GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(metrics))
            {
                foreach (var result in enumerator.Current.Values)
                {
                    if (rowKeyToItem.TryGetValue(result.RowKey, out var item))
                    {
                        if (result.CommitTimestamp >= item.CommitTimestamp)
                        {
                            // The version in Table Storage is newer, ignore the version we have.
                            rowKeyToItem.Remove(result.RowKey);
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
            }

            _updateMetric.TrackValue(updateCount, _leafType);
            _ignoreMetric.TrackValue(ignoreCount, _leafType);
            _wasteMetric.TrackValue(wasteCount, _leafType);
            _addMetric.TrackValue(rowKeyToItem.Count - rowKeyToEtag.Count, _leafType);

            var readRows = ignoreCount + updateCount + wasteCount;
            _wasteRatioMetric.TrackValue(readRows > 0 ? (1.0 * wasteCount) / readRows : 0, _leafType);

            return (rowKeyToItem, rowKeyToEtag);
        }

        private async Task ExecuteBatchAsync(MutableTableTransactionalBatch batch, T lastEntity)
        {
            _logger.LogInformation(
                "Upserting {Count} latest package leaf rows of type {LeafType} with partition key: {PartitionKey}. Last row key: {RowKey}.",
                batch.Count,
                typeof(T).FullName,
                lastEntity.PartitionKey,
                lastEntity.RowKey);
            await batch.SubmitBatchAsync();
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
