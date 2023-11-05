// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.WideEntities
{
    public class WideEntityService
    {
        /// <summary>
        /// 68% of 4 MiB. We use 68% since Base64 encoding of the binary will bring us down to 75% of 4 MiB and we'll
        /// leave the remaining % for the other metadata included in the entity request. Remember, the 4 MiB considers
        /// the entire request body size, not the sum total size of the entities. The request body includes things like
        /// multi-part boundaries, entity identifiers, and unicode escaped '+' signs.
        /// See: https://docs.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions#requirements-for-entity-group-transactions
        /// See: https://github.com/Azure/azure-sdk-for-net/issues/19815
        /// </summary>
        private const int MaxTotalEntitySize = (int)(0.68 * 1024 * 1024 * 4);

        /// <summary>
        /// 64 KiB
        /// See: https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-types
        /// </summary>
        private const int MaxBinaryPropertySize = 64 * 1024;

        /// <summary>
        /// For storage emulator this is 8 segments and 3 for Azure. So we use 8.
        /// </summary>
        private const int MaxSegmentsPerWideEntity = 8;

        /// <summary>
        /// This must be smaller than <see cref="MaxTotalEntitySize"/> to allow for entity overhead and property
        /// overhead per entity. This represents that maximum content length allowed in a wide entity. We use around
        /// </summary>
        public static readonly int MaxTotalDataSize;

        static WideEntityService()
        {
            // We calculate the max total data size by subtracting the largest possible entity overhead size times the
            // maximum number of segments.

            var calculator = new TableEntitySizeCalculator();
            calculator.AddEntityOverhead();
            calculator.AddPartitionKey(512); // Max partition key size = 1 KiB (512 UTF-16 characters)
            calculator.AddRowKey(512); // Max row key size = 1 KiB (512 UTF-16 characters)

            // Add the segment size property.
            calculator.AddPropertyOverhead(1);
            calculator.AddInt32Data();

            // Add the client request ID property.
            calculator.AddPropertyOverhead(1);
            calculator.AddGuidData();

            // We can have up to 16 chunks per entity.
            for (var i = 0; i < WideEntitySegment.ChunkPropertyNames.Count; i++)
            {
                // Add the overhead for a binary property.
                calculator.AddBinaryData(0);
            }

            MaxTotalDataSize = MaxTotalEntitySize - MaxSegmentsPerWideEntity * calculator.Size;
        }

        private const string ContentTooLargeMessage = "The content is too large.";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly int _maxEntitySize;

        public WideEntityService(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsSettings> options)
        {
            _serviceClientFactory = serviceClientFactory ?? throw new ArgumentNullException(nameof(serviceClientFactory));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            if (options.Value.StorageConnectionString == StorageUtility.EmulatorConnectionString)
            {
                // See: https://stackoverflow.com/a/65770156
                _maxEntitySize = (int)(393250 * 0.99);
            }
            else
            {
                // 1 MiB
                // See: https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-limitations
                _maxEntitySize = 1024 * 1024;
            }
        }

        public async Task CreateTableAsync(string tableName)
        {
            await (await GetTableAsync(tableName)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteTableAsync(string tableName)
        {
            await (await GetTableAsync(tableName)).DeleteAsync();
        }

        public async Task<WideEntity> RetrieveAsync(string tableName, string partitionKey, string rowKey)
        {
            return await RetrieveAsync(tableName, partitionKey, rowKey, includeData: true);
        }

        public async Task<WideEntity> RetrieveAsync(string tableName, string partitionKey, string rowKey, bool includeData)
        {
            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            var result = await RetrieveAsync(tableName, partitionKey, rowKey, rowKey, includeData);
            return result.SingleOrDefault();
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName)
        {
            return await RetrieveAsync(tableName, partitionKey: null, minRowKey: null, maxRowKey: null, includeData: true);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, bool includeData)
        {
            return await RetrieveAsync(tableName, partitionKey: null, minRowKey: null, maxRowKey: null, includeData);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey)
        {
            return await RetrieveAsync(tableName, partitionKey, minRowKey: null, maxRowKey: null, includeData: true);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey, bool includeData)
        {
            return await RetrieveAsync(tableName, partitionKey, minRowKey: null, maxRowKey: null, includeData);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey, string minRowKey, string maxRowKey)
        {
            return await RetrieveAsync(tableName, partitionKey, minRowKey, maxRowKey, includeData: true);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey, string minRowKey, string maxRowKey, bool includeData)
        {
            return await RetrieveAsync(tableName, partitionKey, minRowKey, maxRowKey, includeData, maxPerPage: StorageUtility.MaxTakeCount)
                .ToListAsync();
        }

        public async IAsyncEnumerable<WideEntity> RetrieveAsync(string tableName, string partitionKey, string minRowKey, string maxRowKey, bool includeData, int maxPerPage)
        {
            var table = await GetTableAsync(tableName);
            await foreach (var entity in RetrieveAsync(tableName, table, partitionKey, minRowKey, maxRowKey, includeData, maxPerPage))
            {
                yield return entity;
            }
        }

        private async IAsyncEnumerable<WideEntity> RetrieveAsync(string tableName, TableClientWithRetryContext table, string partitionKey, string minRowKey, string maxRowKey, bool includeData, int maxPerPage)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics(dimension1Name: "TableName", dimension1Value: tableName);

            var noRowKeys = false;
            if (minRowKey == null)
            {
                if (maxRowKey != null)
                {
                    throw new ArgumentException("If min row key is null, max row key must be null as well.", nameof(maxRowKey));
                }

                noRowKeys = true;
            }
            else
            {
                if (partitionKey == null)
                {
                    throw new ArgumentException("If the partition key is null, the min and max row keys must be null as well.", nameof(minRowKey));
                }
            }

            if (minRowKey != null && maxRowKey == null)
            {
                throw new ArgumentNullException(nameof(maxRowKey));
            }

            IList<string> selectColumns;
            Expression<Func<WideEntitySegment, bool>> filter;
            if (includeData)
            {
                selectColumns = null;

                if (partitionKey == null)
                {
                    filter = x => true;
                }
                else if (noRowKeys)
                {
                    filter = x => x.PartitionKey == partitionKey;
                }
                else
                {
                    filter = x =>
                        x.PartitionKey == partitionKey
                        && x.RowKey.CompareTo($"{minRowKey}{WideEntitySegment.RowKeySeparator}") >= 0 // Minimum possible row key with this prefix.
                        && x.RowKey.CompareTo($"{maxRowKey}{WideEntitySegment.RowKeySeparator}{char.MaxValue}") <= 0;  // Maximum possible row key with this prefix.
                }
            }
            else
            {
                selectColumns = new[]
                {
                    StorageUtility.PartitionKey,
                    StorageUtility.RowKey,
                    StorageUtility.Timestamp,
                    WideEntitySegment.SegmentCountPropertyName,
                };

                if (partitionKey == null)
                {
                    filter = x => true;
                }
                else if (noRowKeys)
                {
                    filter = x => x.PartitionKey == partitionKey;
                }
                else if (minRowKey == maxRowKey)
                {
                    filter = x =>
                        x.PartitionKey == partitionKey
                        && x.RowKey == $"{minRowKey}{WideEntitySegment.RowKeySeparator}{WideEntitySegment.Index0Suffix}";
                }
                else
                {
                    filter = x =>
                        x.PartitionKey == partitionKey
                        && x.RowKey.CompareTo($"{minRowKey}{WideEntitySegment.RowKeySeparator}") >= 0
                        && x.RowKey.CompareTo($"{maxRowKey}{WideEntitySegment.RowKeySeparator}{WideEntitySegment.Index0Suffix}") <= 0;
                }
            }

            var entities = QueryEntitiesAsync(table, metrics, selectColumns, filter, maxPerPage);
            await foreach (var entity in DeserializeEntitiesAsync(entities, includeData))
            {
                yield return entity;
            }
        }

        private static async IAsyncEnumerable<WideEntitySegment> QueryEntitiesAsync(
            TableClientWithRetryContext table,
            QueryLoopMetrics metrics,
            IList<string> selectColumns,
            Expression<Func<WideEntitySegment, bool>> filter,
            int maxPerPage)
        {
            var query = table.QueryAsync(
                filter,
                maxPerPage: maxPerPage,
                select: selectColumns);
            var enumerator = query.AsPages().GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(metrics))
            {
                var entitySegment = enumerator.Current.Values;
                if (!entitySegment.Any())
                {
                    continue;
                }

                foreach (var entity in entitySegment)
                {
                    yield return entity;
                }
            }
        }

        public static async IAsyncEnumerable<WideEntity> DeserializeEntitiesAsync(IAsyncEnumerable<WideEntitySegment> segments, bool includeData)
        {
            string currentPartitionKey = null;
            string currentRowKeyPrefix = null;
            var currentSegments = new List<WideEntitySegment>();

            await foreach (var entity in segments)
            {
                if (currentPartitionKey == null)
                {
                    currentPartitionKey = entity.PartitionKey;
                    currentRowKeyPrefix = entity.RowKeyPrefix;
                }

                if (entity.PartitionKey == currentPartitionKey && entity.RowKeyPrefix == currentRowKeyPrefix)
                {
                    currentSegments.Add(entity);
                }
                else
                {
                    yield return MakeWideEntity(includeData, currentSegments);
                    currentPartitionKey = entity.PartitionKey;
                    currentRowKeyPrefix = entity.RowKeyPrefix;
                    currentSegments.Clear();
                    currentSegments.Add(entity);
                }
            }

            if (currentSegments.Any())
            {
                yield return MakeWideEntity(includeData, currentSegments);
            }
        }

        public async Task<IReadOnlyList<WideEntity>> ExecuteBatchAsync(string tableName, IEnumerable<WideEntityOperation> batch, bool allowBatchSplits)
        {
            var table = await GetTableAsync(tableName);
            var tableBatch = new MutableTableTransactionalBatch(table);
            var segmentsList = new List<List<WideEntitySegment>>();
            foreach (var operation in batch)
            {
                // Keep track of the number of table operations in the batch prior to adding the next wide entity
                // operation, just in case we have to remove the operations for a batch split.
                var previousOperationCount = tableBatch.Count;
                switch (operation)
                {
                    case WideEntityInsertOperation insert:
                        segmentsList.Add(await AddInsertAsync(insert.PartitionKey, insert.RowKey, insert.Content, tableBatch));
                        break;
                    case WideEntityReplaceOperation replace:
                        segmentsList.Add(await AddReplaceAsync(replace.Existing, replace.Content, tableBatch));
                        break;
                    case WideEntityInsertOrReplaceOperation insertOrReplace:
                        segmentsList.Add(await AddInsertOrReplaceAsync(tableName, insertOrReplace.PartitionKey, insertOrReplace.RowKey, insertOrReplace.Content, tableBatch));
                        break;
                    case WideEntityDeleteOperation delete:
                        AddDelete(delete.Existing, tableBatch);
                        segmentsList.Add(null);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (allowBatchSplits
                    && tableBatch.Count > previousOperationCount
                    && previousOperationCount > 0
                    && segmentsList.Sum(x => x != null ? x.Sum(y => y.GetEntitySize()) : 0) > MaxTotalEntitySize)
                {
                    // Remove the table operations added by this wide entity operation since it made the batch too large.
                    var addedOperations = new Stack<TableTransactionOperation>();
                    while (tableBatch.Count > previousOperationCount)
                    {
                        addedOperations.Push(tableBatch.Last());
                        tableBatch.RemoveAt(tableBatch.Count - 1);
                    }

                    // Execute the batch, now that it is within the size limit.
                    await tableBatch.SubmitBatchAsync();

                    // Start a new batch with the table operations associated with this entity operation.
                    tableBatch.Clear();
                    while (addedOperations.Any())
                    {
                        tableBatch.Add(addedOperations.Pop());
                    }
                }
            }

            await tableBatch.SubmitBatchIfNotEmptyAsync();

            var output = new List<WideEntity>();
            foreach (var segments in segmentsList)
            {
                if (segments == null)
                {
                    output.Add(null);
                }
                else
                {
                    output.Add(new WideEntity(segments));
                }
            }

            return output;
        }

        public async Task DeleteAsync(string tableName, WideEntity existing)
        {
            var table = await GetTableAsync(tableName);
            var batch = new MutableTableTransactionalBatch(table);
            AddDelete(existing, batch);
            await batch.SubmitBatchAsync();
        }

        private static void AddDelete(WideEntity existing, MutableTableTransactionalBatch batch)
        {
            // Use the etag on the first entity, for optimistic concurrency.
            batch.DeleteEntity(existing.PartitionKey, WideEntitySegment.GetRowKey(existing.RowKey, index: 0), existing.ETag);

            for (var index = 1; index < existing.SegmentCount; index++)
            {
                batch.DeleteEntity(existing.PartitionKey, WideEntitySegment.GetRowKey(existing.RowKey, index), ETag.All);
            }
        }

        public async Task<WideEntity> ReplaceAsync(string tableName, WideEntity existing, ReadOnlyMemory<byte> content)
        {
            var table = await GetTableAsync(tableName);
            var batch = new MutableTableTransactionalBatch(table);
            var segments = await AddReplaceAsync(existing, content, batch);
            await batch.SubmitBatchAsync();
            return new WideEntity(segments);
        }

        private async Task<List<WideEntitySegment>> AddReplaceAsync(
            WideEntity existing,
            ReadOnlyMemory<byte> content,
            MutableTableTransactionalBatch batch)
        {
            return await AddInsertOrReplaceAsync(
                batch,
                () => Task.FromResult(existing),
                existing.PartitionKey,
                existing.RowKey,
                content);
        }

        public async Task<WideEntity> InsertAsync(string tableName, string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
        {
            var table = await GetTableAsync(tableName);
            var batch = new MutableTableTransactionalBatch(table);
            var segments = await AddInsertAsync(partitionKey, rowKey, content, batch);
            await batch.SubmitBatchAsync();
            return new WideEntity(segments);
        }

        private async Task<List<WideEntitySegment>> AddInsertAsync(
            string partitionKey,
            string rowKey,
            ReadOnlyMemory<byte> content,
            MutableTableTransactionalBatch batch)
        {
            return await AddInsertOrReplaceAsync(
                batch,
                () => Task.FromResult<WideEntity>(null),
                partitionKey,
                rowKey,
                content);
        }

        public async Task<WideEntity> InsertOrReplaceAsync(string tableName, string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
        {
            var table = await GetTableAsync(tableName);
            var batch = new MutableTableTransactionalBatch(table);
            var segments = await AddInsertOrReplaceAsync(tableName, partitionKey, rowKey, content, batch);
            await batch.SubmitBatchAsync();
            return new WideEntity(segments);
        }

        private Task<List<WideEntitySegment>> AddInsertOrReplaceAsync(
            string tableName,
            string partitionKey,
            string rowKey,
            ReadOnlyMemory<byte> content,
            MutableTableTransactionalBatch batch)
        {
            return AddInsertOrReplaceAsync(
                batch,
                async () => (await RetrieveAsync(
                    tableName,
                    batch.TableClient,
                    partitionKey,
                    rowKey,
                    rowKey,
                    includeData: false,
                    maxPerPage: StorageUtility.MaxTakeCount).ToListAsync()).SingleOrDefault(),
                partitionKey,
                rowKey,
                content);
        }

        private async Task<List<WideEntitySegment>> AddInsertOrReplaceAsync(
            MutableTableTransactionalBatch batch,
            Func<Task<WideEntity>> getExistingAsync,
            string partitionKey,
            string rowKey,
            ReadOnlyMemory<byte> content)
        {
            if (content.Length > MaxTotalDataSize)
            {
                throw new ArgumentException(ContentTooLargeMessage, nameof(content));
            }

            var segments = MakeSegments(partitionKey, rowKey, content);

            var existing = await getExistingAsync();
            if (existing == null)
            {
                foreach (var segment in segments)
                {
                    batch.AddEntity(segment);
                }
            }
            else
            {
                // Use the etag on the first entity, for optimistic concurrency.
                batch.UpdateEntity(segments[0], existing.ETag, mode: TableUpdateMode.Replace);

                // Blindly insert or replace the rest of the new segments, ignoring etags.
                foreach (var segment in segments.Skip(1))
                {
                    batch.UpsertEntity(segment, mode: TableUpdateMode.Replace);
                }

                // Delete any extra segments existing on the old entity but not on the new one.
                for (var index = segments.Count; index < existing.SegmentCount; index++)
                {
                    batch.DeleteEntity(partitionKey, WideEntitySegment.GetRowKey(existing.RowKey, index), ETag.All);
                }
            }

            return segments;
        }

        private static WideEntity MakeWideEntity(bool includeData, List<WideEntitySegment> segments)
        {
            if (includeData)
            {
                return new WideEntity(segments);
            }
            else
            {
                return new WideEntity(segments[0]);
            }
        }

        private List<WideEntitySegment> MakeSegments(string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
        {
            var segments = new List<WideEntitySegment>();
            if (content.Length == 0)
            {
                segments.Add(new WideEntitySegment(partitionKey, rowKey, index: 0));
            }
            else
            {
                var calculator = new TableEntitySizeCalculator();
                calculator.AddEntityOverhead();
                calculator.AddPartitionKey(partitionKey);
                calculator.AddRowKey(rowKey.Length + 1 + 2); // 1 for the separator, 2 for the index, e.g. "~02"
                var entityOverhead = calculator.Size;
                calculator.Reset();

                calculator.AddPropertyOverhead(1); // 1 because the property names are a single character.
                var propertyOverhead = calculator.Size;
                calculator.Reset();

                // Now, we drain the total entity size by filling with max size entities. Each max size entity will contain
                // chunks of the data.
                var remainingTotalEntitySize = MaxTotalEntitySize;
                var dataStart = 0;
                while (dataStart < content.Length)
                {
                    (var segment, var segmentSize) = MakeSegmentOrNull(
                        partitionKey,
                        rowKey,
                        index: segments.Count,
                        entityOverhead,
                        propertyOverhead,
                        maxEntitySize: Math.Min(_maxEntitySize, remainingTotalEntitySize),
                        ref dataStart,
                        content);

                    if (segment == null)
                    {
                        throw new ArgumentException(ContentTooLargeMessage, nameof(content));
                    }

                    remainingTotalEntitySize -= segmentSize;
                    segments.Add(segment);
                }
            }

            segments.First().SegmentCount = segments.Count;

            return segments;
        }

        private (WideEntitySegment Segment, int SegmentSize) MakeSegmentOrNull(
            string partitionKey,
            string rowKey,
            int index,
            int entityOverhead,
            int propertyOverhead,
            int maxEntitySize,
            ref int dataStart,
            ReadOnlyMemory<byte> data)
        {
            WideEntitySegment segment = null;

            var remainingEntitySize = maxEntitySize - entityOverhead;
            var binaryPropertyOverhead = propertyOverhead + 4;

            if (index == 0)
            {
                // Account for the segment count property: property name plus the integer
                remainingEntitySize -= propertyOverhead + 4;
            }

            while (dataStart < data.Length && remainingEntitySize > 0)
            {
                var binarySize = Math.Min(remainingEntitySize - binaryPropertyOverhead, Math.Min(data.Length - dataStart, MaxBinaryPropertySize));
                if (binarySize <= 0)
                {
                    break;
                }

                // Account for the chunk property name and chunk length
                remainingEntitySize -= binaryPropertyOverhead;

                if (segment == null)
                {
                    segment = new WideEntitySegment(partitionKey, rowKey, index);
                }

                segment.AddChunk(data.Slice(dataStart, binarySize));
                dataStart += binarySize;

                // Account for the chunk data
                remainingEntitySize -= binarySize;
            }

            var segmentSize = maxEntitySize - remainingEntitySize;
            if (segment != null)
            {
                var actual = segment.GetEntitySize();
                if (actual != segmentSize)
                {
                    throw new InvalidOperationException($"The segment size calculation is incorrect. Expected: {segmentSize}. Actual: {actual}.");
                }
            }

            return (segment, segmentSize);
        }

        private async Task<TableClientWithRetryContext> GetTableAsync(string tableName)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(tableName);
        }
    }
}
