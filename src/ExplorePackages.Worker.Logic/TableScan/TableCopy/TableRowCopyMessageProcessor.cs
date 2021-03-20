using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableRowCopyMessageProcessor<T> : IMessageProcessor<TableRowCopyMessage<T>> where T : ITableEntity, new()
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TableRowCopyMessageProcessor<T>> _logger;

        public TableRowCopyMessageProcessor(
            ServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            ILogger<TableRowCopyMessageProcessor<T>> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task ProcessAsync(TableRowCopyMessage<T> message, long dequeueCount)
        {
            var rows = await GetSourceRowsAsync(message);

            var destinationTable = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(message.DestinationTableName);

            await destinationTable.InsertOrReplaceEntitiesAsync(rows);
        }

        private async Task<List<T>> GetSourceRowsAsync(TableRowCopyMessage<T> message)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var sourceTable = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(message.SourceTableName);

            var sortedRowKeys = message.RowKeys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var minRowKey = sortedRowKeys.First();
            var maxRowKey = sortedRowKeys.Last();
            var rowKeys = message.RowKeys.ToHashSet();

            var query = new TableQuery<T>
            {
                FilterString = TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition(
                        StorageUtility.PartitionKey,
                        QueryComparisons.Equal,
                        message.PartitionKey),
                    TableOperators.And,
                    TableQuery.CombineFilters(
                        TableQuery.GenerateFilterCondition(
                            StorageUtility.RowKey,
                            QueryComparisons.GreaterThanOrEqual,
                            minRowKey),
                        TableOperators.And,
                        TableQuery.GenerateFilterCondition(
                            StorageUtility.RowKey,
                            QueryComparisons.LessThanOrEqual,
                            maxRowKey))),
                TakeCount = StorageUtility.MaxTakeCount,
            };

            var rows = new List<T>();
            TableContinuationToken continuationToken = null;
            do
            {
                TableQuerySegment<T> segment;
                using (metrics.TrackQuery())
                {
                    segment = await sourceTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                }

                foreach (var row in segment.Results)
                {
                    if (rowKeys.Contains(row.RowKey))
                    {
                        rows.Add(row);
                    }
                }
                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);

            if (rows.Count != rowKeys.Count)
            {
                _logger.LogError(
                    "In partition key {PartitionKey}, some row keys were not found: {MissingRowKeys}",
                    message.PartitionKey,
                    rowKeys.Except(rows.Select(x => x.RowKey)).ToList());
                throw new InvalidOperationException("When copying rows, some rows were not found.");
            }

            return rows;
        }
    }
}
