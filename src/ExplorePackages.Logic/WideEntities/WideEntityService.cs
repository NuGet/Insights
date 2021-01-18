using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityService
    {
        /// <summary>
        /// 70% of 4 MiB. We use 70% since Base64 encoding of the binary will bring us down to 75% of 4 MiB and we'll
        /// leave the remaining 5% for the other metadata included in the entity request. Remember, the 4 MiB considers
        /// the entire request body size, not the sum total size of the entities. The request body includes things like
        /// multi-part boundaries and entity identifiers.
        /// See: https://docs.microsoft.com/en-us/rest/api/storageservices/performing-entity-group-transactions#requirements-for-entity-group-transactions
        /// </summary>
        public const int MaxTotalEntitySize = (int)(0.70 * 1024 * 1024 * 4);

        /// <summary>
        /// 64 KiB
        /// See: https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-types
        /// </summary>
        private const int MaxBinaryPropertySize = 64 * 1024;

        /// <summary>
        /// The separator between the user-provided wide entity row key and the wide entity index suffix.
        /// </summary>
        internal const char RowKeySeparator = '~';

        private const string ContentTooLargetMessage = "The content is too large.";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly int _maxEntitySize;

        public WideEntityService(ServiceClientFactory serviceClientFactory)
        {
            _serviceClientFactory = serviceClientFactory ?? throw new ArgumentNullException(nameof(serviceClientFactory));

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

        public async Task<Stream> GetAsync(string tableName, string partitionKey, string rowKey)
        {
            var query = new TableQuery<WideEntitySegment>
            {
                FilterString = TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(
                        StorageUtility.PartitionKey,
                        QueryComparisons.Equal,
                        partitionKey),
                    TableOperators.And,
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition(
                            StorageUtility.RowKey,
                            QueryComparisons.GreaterThanOrEqual,
                            $"{rowKey}{RowKeySeparator}"), // Minimum possible row key with this prefix.
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition(
                            StorageUtility.RowKey,
                            QueryComparisons.LessThanOrEqual,
                            $"{rowKey}{RowKeySeparator}{char.MaxValue}"))), // Maximum possible row key with this prefix.
                TakeCount = StorageUtility.MaxTakeCount,
            };

            var table = GetTable(tableName);

            var entitySegment = await table.ExecuteQuerySegmentedAsync(query, token: null);
            if (entitySegment.ContinuationToken != null)
            {
                throw new InvalidDataException("There is a continuation token for fetching a wide entity. This indicates that there too many entities.");
            }

            var memoryStream = new MemoryStream();
            foreach (var entity in entitySegment)
            {
                foreach (var chunk in entity.Chunks)
                {
                    memoryStream.Write(chunk.Span);
                }
            }

            memoryStream.Position = 0;

            return memoryStream;
        }

        public async Task InsertAsync(string tableName, string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
        {
            if (content.Length > MaxTotalEntitySize)
            {
                throw new ArgumentException(ContentTooLargetMessage, nameof(content));
            }

            var segments = MakeSegments(partitionKey, rowKey, content);

            var table = GetTable(tableName);
            var batch = new TableBatchOperation();
            foreach (var segment in segments)
            {
                batch.Add(TableOperation.Insert(segment));
            }

            await table.ExecuteBatchAsync(batch);
        }

        private List<WideEntitySegment> MakeSegments(string partitionKey, string rowKey, ReadOnlyMemory<byte> content)
        {
            var segments = new List<WideEntitySegment>();
            if (content.Length == 0)
            {
                segments.Add(new WideEntitySegment(partitionKey, rowKey, 0));
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
                // chunks of the compressed data.
                var remainingTotalEntitySize = MaxTotalEntitySize;
                var dataStart = 0;
                while (dataStart < content.Length)
                {
                    var segment = MakeSegmentOrNull(
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
                        throw new ArgumentException(ContentTooLargetMessage, nameof(content));
                    }

                    segments.Add(segment);
                }
            }

            return segments;
        }

        private WideEntitySegment MakeSegmentOrNull(
            string partitionKey,
            string rowKey,
            int index,
            int entityOverhead,
            int propertyOverhead,
            int maxEntitySize,
            ref int dataStart,
            ReadOnlyMemory<byte> data)
        {
            WideEntitySegment wideEntity = null;
            var remainingEntitySize = maxEntitySize;
            while (dataStart < data.Length && remainingEntitySize > 0)
            {
                remainingEntitySize -= entityOverhead + propertyOverhead;

                var binarySize = Math.Min(remainingEntitySize, Math.Min(data.Length - dataStart, MaxBinaryPropertySize));
                if (binarySize <= 0)
                {
                    break;
                }

                if (wideEntity == null)
                {
                    wideEntity = new WideEntitySegment(partitionKey, rowKey, index);
                }

                wideEntity.Chunks.Add(data.Slice(dataStart, binarySize));
                dataStart += binarySize;
                remainingEntitySize -= binarySize;
            }

            return wideEntity;
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
