using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityService
    {
        /// <summary>
        /// 72% of 4 MiB. We use 72% since Base64 encoding of the binary will bring us down to 75% of 4 MiB and we'll
        /// leave the remaining 3% for the other metadata included in the entity request. Remember, the 4 MiB considers
        /// the entire request body size, not the sum total size of the entities. The request body includes things like
        /// multi-part boundaries and entity identifiers.
        /// See: https://docs.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions#requirements-for-entity-group-transactions
        /// </summary>
        private const int MaxTotalEntitySize = (int)(0.72 * 1024 * 1024 * 4);

        /// <summary>
        /// 64 KiB
        /// See: https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-types
        /// </summary>
        private const int MaxBinaryPropertySize = 64 * 1024;

        /// <summary>
        /// This must be smaller than <see cref="MaxTotalEntitySize"/> to allow for entity overhead and property
        /// overhead per entity. This represents that maximum content length allowed in a wide entity. We use around
        /// </summary>
        public static readonly int MaxTotalDataSize;

        static WideEntityService()
        {
            // We calculate the max total data size by subtracting the largest possible entity overhead size times the
            // maximum number of segments. For storage emulator this is 8 segments and 3 for Azure. So we use 8.

            var calculator = new TableEntitySizeCalculator();
            calculator.AddEntityOverhead();
            calculator.AddPartitionKey(512); // Max partition key size = 1 KiB (512 UTF-16 characters)
            calculator.AddRowKey(512); // Max row key size = 1 KiB (512 UTF-16 characters)

            // Add the segment size property.
            calculator.AddPropertyOverhead(1);
            calculator.AddInt32Data();

            // We can have up to 16 chunks per entity.
            for (var i = 0; i < 16; i++)
            {
                // Add the overhead for a binary property.
                calculator.AddBinaryData(0);
            }

            MaxTotalDataSize = MaxTotalEntitySize - 8 * calculator.Size;
        }

        private const string ContentTooLargeMessage = "The content is too large.";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly int _maxEntitySize;

        public WideEntityService(ServiceClientFactory serviceClientFactory, ITelemetryClient telemetryClient)
        {
            _serviceClientFactory = serviceClientFactory ?? throw new ArgumentNullException(nameof(serviceClientFactory));
            _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));

            if (_serviceClientFactory.GetStorageAccount().TableEndpoint.IsLoopback)
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

        public async Task InitializeAsync(string tableName)
        {
            await GetTable(tableName).CreateIfNotExistsAsync(retry: true);
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

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey)
        {
            return await RetrieveAsync(tableName, partitionKey, minRowKey: null, maxRowKey: null, includeData: true);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey, string minRowKey, string maxRowKey)
        {
            return await RetrieveAsync(tableName, partitionKey, minRowKey, maxRowKey, includeData: true);
        }

        public async Task<IReadOnlyList<WideEntity>> RetrieveAsync(string tableName, string partitionKey, string minRowKey, string maxRowKey, bool includeData)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics(memberName: tableName + nameof(RetrieveAsync));

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
            string filterString;
            if (includeData)
            {
                selectColumns = null;
                if (partitionKey == null)
                {
                    filterString = null;
                }
                else if (noRowKeys)
                {
                    filterString = PK_EQ(partitionKey);
                }
                else
                {
                    filterString = TableQuery.CombineFilters(
                        PK_EQ(partitionKey),
                        TableOperators.And,
                        TableQuery.CombineFilters(
                            RK_GTE(minRowKey, string.Empty), // Minimum possible row key with this prefix.
                            TableOperators.And,
                            RK_LTE(maxRowKey, char.MaxValue.ToString()))); // Maximum possible row key with this prefix.
                }
            }
            else
            {
                selectColumns = new[] { WideEntitySegment.SegmentCountPropertyName };
                if (partitionKey == null)
                {
                    filterString = null;
                }
                else if (noRowKeys)
                {
                    filterString = PK_EQ(partitionKey);
                }
                else if (minRowKey == maxRowKey)
                {
                    filterString = TableQuery.CombineFilters(
                        PK_EQ(partitionKey),
                        TableOperators.And,
                        RK_EQ(minRowKey, WideEntitySegment.Index0Suffix));
                }
                else
                {
                    filterString = TableQuery.CombineFilters(
                        PK_EQ(partitionKey),
                        TableOperators.And,
                        TableQuery.CombineFilters(
                            RK_GTE(minRowKey, string.Empty),
                            TableOperators.And,
                            RK_LTE(maxRowKey, WideEntitySegment.Index0Suffix)));
                }
            }

            var query = new TableQuery<WideEntitySegment>
            {
                FilterString = filterString,
                SelectColumns = selectColumns,
                TakeCount = StorageUtility.MaxTakeCount,
            };

            var table = GetTable(tableName);
            var output = new List<WideEntity>();

            string currentPartitionKey = null;
            string currentRowKeyPrefix = null;
            var segments = new List<WideEntitySegment>();
            TableContinuationToken token = null;

            do
            {
                TableQuerySegment<WideEntitySegment> entitySegment;
                using (metrics.TrackQuery())
                {
                    entitySegment = await table.ExecuteQuerySegmentedAsync(query, token);
                }

                token = entitySegment.ContinuationToken;

                if (!entitySegment.Results.Any())
                {
                    continue;
                }

                if (currentPartitionKey == null)
                {
                    currentPartitionKey = entitySegment.Results.First().PartitionKey;
                    currentRowKeyPrefix = entitySegment.Results.First().RowKeyPrefix;
                }

                foreach (var entity in entitySegment.Results)
                {
                    if (entity.PartitionKey == currentPartitionKey && entity.RowKeyPrefix == currentRowKeyPrefix)
                    {
                        segments.Add(entity);
                    }
                    else
                    {
                        MakeWideEntity(includeData, output, segments);
                        currentPartitionKey = entity.PartitionKey;
                        currentRowKeyPrefix = entity.RowKeyPrefix;
                        segments.Clear();
                        segments.Add(entity);
                    }
                }
            }
            while (token != null);

            if (segments.Any())
            {
                MakeWideEntity(includeData, output, segments);
            }

            return output;
        }

        public async Task<IReadOnlyList<WideEntity>> ExecuteBatchAsync(string tableName, IEnumerable<WideEntityOperation> batch, bool allowBatchSplits)
        {
            var tableOperationBatch = new TableBatchOperation();
            var segmentsList = new List<List<WideEntitySegment>>();
            foreach (var operation in batch)
            {
                // Keep track of the number of table operations in the batch prior to adding the next wide entity
                // operation, just in case we have to remove the operations for a batch split.
                var previousOperationCount = tableOperationBatch.Count;
                switch (operation)
                {
                    case WideEntityInsertOperation insert:
                        segmentsList.Add(await AddInsertAsync(insert.PartitionKey, insert.RowKey, insert.Content, tableOperationBatch));
                        break;
                    case WideEntityReplaceOperation replace:
                        segmentsList.Add(await AddReplaceAsync(replace.Existing, replace.Content, tableOperationBatch));
                        break;
                    case WideEntityInsertOrReplaceOperation insertOrReplace:
                        segmentsList.Add(await AddInsertOrReplaceAsync(tableName, insertOrReplace.PartitionKey, insertOrReplace.RowKey, insertOrReplace.Content, tableOperationBatch));
                        break;
                    case WideEntityDeleteOperation delete:
                        AddDelete(delete.Existing, tableOperationBatch);
                        segmentsList.Add(null);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                if (allowBatchSplits
                    && tableOperationBatch.Count > previousOperationCount
                    && previousOperationCount > 0
                    && segmentsList.Sum(x => x.Sum(y => y.GetEntitySize())) > MaxTotalEntitySize)
                {
                    // Remove the table operations added by this wide entity operation since it made the batch too large.
                    var addedOperations = new Stack<TableOperation>();
                    while (tableOperationBatch.Count > previousOperationCount)
                    {
                        addedOperations.Push(tableOperationBatch.Last());
                        tableOperationBatch.RemoveAt(tableOperationBatch.Count - 1);
                    }

                    // Execute the batch, now that it is within the size limit.
                    await GetTable(tableName).ExecuteBatchAsync(tableOperationBatch);

                    // Start a new batch with the table operations associated with this entity operation.
                    tableOperationBatch.Clear();
                    while (addedOperations.Any())
                    {
                        tableOperationBatch.Add(addedOperations.Pop());
                    }
                }
            }

            if (tableOperationBatch.Any())
            {
                await GetTable(tableName).ExecuteBatchAsync(tableOperationBatch);
            }

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
            var batch = new TableBatchOperation();
            AddDelete(existing, batch);
            await GetTable(tableName).ExecuteBatchAsync(batch);
        }

        private static void AddDelete(WideEntity existing, TableBatchOperation batch)
        {
            // Use the etag on the first entity, for optimistic concurrency.
            batch.Add(TableOperation.Delete(new WideEntitySegment(existing.PartitionKey, existing.RowKey, 0) { ETag = existing.ETag }));

            for (var index = 1; index < existing.SegmentCount; index++)
            {
                batch.Add(TableOperation.Delete(new WideEntitySegment(existing.PartitionKey, existing.RowKey, index) { ETag = "*" }));
            }
        }

        public async Task<WideEntity> ReplaceAsync(string tableName, WideEntity existing, ReadOnlyMemory<byte> content)
        {
            var batch = new TableBatchOperation();
            var segments = await AddReplaceAsync(existing, content, batch);
            await GetTable(tableName).ExecuteBatchAsync(batch);
            return new WideEntity(segments);
        }

        private async Task<List<WideEntitySegment>> AddReplaceAsync(WideEntity existing, ReadOnlyMemory<byte> content, TableBatchOperation batch)
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
            var batch = new TableBatchOperation();
            var segments = await AddInsertAsync(partitionKey, rowKey, content, batch);
            await GetTable(tableName).ExecuteBatchAsync(batch);
            return new WideEntity(segments);
        }

        private async Task<List<WideEntitySegment>> AddInsertAsync(string partitionKey, string rowKey, ReadOnlyMemory<byte> content, TableBatchOperation batch)
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
            var batch = new TableBatchOperation();
            var segments = await AddInsertOrReplaceAsync(tableName, partitionKey, rowKey, content, batch);
            await GetTable(tableName).ExecuteBatchAsync(batch);
            return new WideEntity(segments);
        }

        private Task<List<WideEntitySegment>> AddInsertOrReplaceAsync(string tableName, string partitionKey, string rowKey, ReadOnlyMemory<byte> content, TableBatchOperation batch)
        {
            return AddInsertOrReplaceAsync(
                batch,
                () => RetrieveAsync(tableName, partitionKey, rowKey, includeData: false),
                partitionKey,
                rowKey,
                content);
        }

        private async Task<List<WideEntitySegment>> AddInsertOrReplaceAsync(
            TableBatchOperation batch,
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
                    batch.Add(TableOperation.Insert(segment));
                }
            }
            else
            {
                // Use the etag on the first entity, for optimistic concurrency.
                segments[0].ETag = existing.ETag;
                batch.Add(TableOperation.Replace(segments[0]));

                // Blindly insert or replace the rest of the new segments, ignoring etags.
                foreach (var segment in segments.Skip(1))
                {
                    batch.Add(TableOperation.InsertOrReplace(segment));
                }

                // Delete any extra segments existing on the old entity but not on the new one.
                for (var index = segments.Count; index < existing.SegmentCount; index++)
                {
                    batch.Add(TableOperation.Delete(new WideEntitySegment(partitionKey, rowKey, index) { ETag = "*" }));
                }
            }

            return segments;
        }

        private static void MakeWideEntity(bool includeData, List<WideEntity> output, List<WideEntitySegment> segments)
        {
            if (includeData)
            {
                output.Add(new WideEntity(segments));
            }
            else
            {
                output.Add(new WideEntity(segments[0]));
            }
        }

        private static string PK_EQ(string partitionKey)
        {
            return TableQuery.GenerateFilterCondition(
                StorageUtility.PartitionKey,
                QueryComparisons.Equal,
                partitionKey);
        }

        private static string RK_LTE(string rowKeyPrefix, string suffix)
        {
            return TableQuery.GenerateFilterCondition(
                StorageUtility.RowKey,
                QueryComparisons.LessThanOrEqual,
                $"{rowKeyPrefix}{WideEntitySegment.RowKeySeparator}{suffix}");
        }

        private static string RK_GTE(string rowKeyPrefix, string suffix)
        {
            return TableQuery.GenerateFilterCondition(
                StorageUtility.RowKey,
                QueryComparisons.GreaterThanOrEqual,
                $"{rowKeyPrefix}{WideEntitySegment.RowKeySeparator}{suffix}");
        }

        private static string RK_EQ(string rowKeyPrefix, string suffix)
        {
            return TableQuery.GenerateFilterCondition(
                StorageUtility.RowKey,
                QueryComparisons.Equal,
                $"{rowKeyPrefix}{WideEntitySegment.RowKeySeparator}{suffix}");
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
                // Account for the segment count entity: property name plus the integer
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

                segment.Chunks.Add(data.Slice(dataStart, binarySize));
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
                    throw new InvalidOperationException($"The segment size calculation is incorrect. Expected: {segment}. Actual: {actual}.");
                }
            }

            return (segment, segmentSize);
        }

        private CloudTable GetTable(string tableName)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(tableName);
        }
    }
}
