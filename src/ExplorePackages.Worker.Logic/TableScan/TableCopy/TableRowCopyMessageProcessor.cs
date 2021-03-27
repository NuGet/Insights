using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableRowCopyMessageProcessor<T> : IMessageProcessor<TableRowCopyMessage<T>> where T : class, ITableEntity, new()
    {
        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TableRowCopyMessageProcessor<T>> _logger;

        public TableRowCopyMessageProcessor(
            NewServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            ILogger<TableRowCopyMessageProcessor<T>> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task ProcessAsync(TableRowCopyMessage<T> message, long dequeueCount)
        {
            var client = await _serviceClientFactory.GetTableServiceClientAsync();
            var sourceTable = client.GetTableClient(message.SourceTableName);

            var rows = await GetSourceRowsAsync(sourceTable, message);

            var destinationTable = client.GetTableClient(message.DestinationTableName);

            var batch = new MutableTableTransactionalBatch(destinationTable);
            foreach (var entity in rows)
            {
                if (batch.Count >= StorageUtility.MaxBatchSize)
                {
                    await batch.SubmitBatchAsync();
                    batch = new MutableTableTransactionalBatch(destinationTable);
                }

                batch.UpsertEntity(entity, mode: TableUpdateMode.Replace);
            }

            await batch.SubmitBatchIfNotEmptyAsync();
        }

        private async Task<List<T>> GetSourceRowsAsync(TableClient sourceTable, TableRowCopyMessage<T> message)
        {
            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var sortedRowKeys = message.RowKeys.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var minRowKey = sortedRowKeys.First();
            var maxRowKey = sortedRowKeys.Last();
            var rowKeys = message.RowKeys.ToHashSet();

            var query = sourceTable.QueryAsync<T>(
                x => x.PartitionKey == message.PartitionKey && x.RowKey.CompareTo(minRowKey) >= 0 && x.RowKey.CompareTo(maxRowKey) <= 0,
                maxPerPage: StorageUtility.MaxTakeCount);

            var rows = new List<T>();
            await using var enumerator = query.AsPages().GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(metrics))
            {
                foreach (var row in enumerator.Current.Values)
                {
                    if (rowKeys.Contains(row.RowKey))
                    {
                        rows.Add(row);
                    }
                }
            }

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
