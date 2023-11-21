// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using static NuGet.Insights.StorageUtility;

namespace NuGet.Insights.Worker
{
    public class LatestLeafStorageService<T> where T : class, ILatestPackageLeaf, new()
    {
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<LatestLeafStorageService<T>> _logger;

        public LatestLeafStorageService(
            ITelemetryClient telemetryClient,
            ILogger<LatestLeafStorageService<T>> logger)
        {
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task AddAsync(
            IReadOnlyList<ICatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage,
            bool allowRetries)
        {
            foreach (var group in items.GroupBy(storage.GetPartitionKey))
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

        private async Task AddAsync(string partitionKey, IReadOnlyList<ICatalogLeafItem> items, ILatestPackageLeafStorage<T> storage)
        {
            (var rowKeyToItem, var rowKeyToETag) = await GetExistingsRowsAsync(partitionKey, items, storage);

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
                var entity = await storage.MapAsync(leaf);
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
            IEnumerable<ICatalogLeafItem> items,
            ILatestPackageLeafStorage<T> storage)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            // Sort items by lexicographical order, since this is what table storage does.
            var itemList = items
                .Select(x => new { Item = x, RowKey = storage.GetRowKey(x) })
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
                    select: new List<string> { RowKey, storage.CommitTimestampColumnName })
                .AsPages();
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
                        }
                        else
                        {
                            // The version in Table Storage is older, save the etag to update it.
                            rowKeyToEtag.Add(result.RowKey, result.ETag);
                        }
                    }
                }
            }

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
    }
}
