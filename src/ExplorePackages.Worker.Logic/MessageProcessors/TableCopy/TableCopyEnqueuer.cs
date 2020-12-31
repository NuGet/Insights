using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyEnqueuer<T> where T : ITableEntity, new()
    {
        private readonly MessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly ILogger<TableCopyEnqueuer<T>> _logger;

        public TableCopyEnqueuer(
            MessageEnqueuer enqueuer,
            SchemaSerializer serializer,
            ILogger<TableCopyEnqueuer<T>> logger)
        {
            _enqueuer = enqueuer;
            _serializer = serializer;
            _logger = logger;
        }

        public async Task StartSerialAsync(string sourceTableName, string destinationTableName)
        {
            await _enqueuer.EnqueueAsync(new[]
            {
                new TableCopyMessage<T>
                {
                    SourceTableName = sourceTableName,
                    DestinationTableName = destinationTableName,
                    Strategy = TableCopyStrategy.Serial,
                },
            });
        }

        public async Task StartPrefixScanAsync(string sourceTableName, string destinationTableName, string partitionKeyPrefix, int takeCount)
        {
            await _enqueuer.EnqueueAsync(new[]
            {
                GetPrefixScanMessage(sourceTableName, destinationTableName, new TablePrefixScanStartParameters
                {
                    PartitionKeyPrefix = partitionKeyPrefix,
                    TakeCount = takeCount,
                }),
            });
        }

        public async Task EnqueuePrefixScanStepsAsync(string destinationTableName, List<TablePrefixScanStep> nextSteps)
        {
            // Two types of messages can be enqueued here:
            //   1. Table row copy messages (the actual work to be done)
            //   2. Table copy messages (recursion)

            var entities = new List<TableEntity>();
            var tableCopyMessages = new List<TableCopyMessage<T>>();

            foreach (var nextStep in nextSteps)
            {
                switch (nextStep)
                {
                    case TablePrefixScanEntitySegment<TableEntity> segment:
                        entities.AddRange(segment.Entities);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        tableCopyMessages.Add(GetPrefixScanMessage(partitionKeyQuery.Parameters.Table.Name, destinationTableName, new TablePrefixScanPartitionKeyQueryParameters
                        {
                            TakeCount = partitionKeyQuery.Parameters.TakeCount,
                            Depth = partitionKeyQuery.Depth,
                            PartitionKey = partitionKeyQuery.PartitionKey,
                            RowKeySkip = partitionKeyQuery.RowKeySkip,
                        }));
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        tableCopyMessages.Add(GetPrefixScanMessage(prefixQuery.Parameters.Table.Name, destinationTableName, new TablePrefixScanPrefixQueryParameters
                        {
                            TakeCount = prefixQuery.Parameters.TakeCount,
                            Depth = prefixQuery.Depth,
                            PartitionKeyPrefix = prefixQuery.PartitionKeyPrefix,
                            PartitionKeyLowerBound = prefixQuery.PartitionKeyLowerBound,
                        }));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            if (entities.Any())
            {
                var sourceTableName = nextSteps.First().Parameters.Table.Name;
                await EnqueueRowCopyAsync(sourceTableName, destinationTableName, entities);
            }

            await _enqueuer.EnqueueAsync(tableCopyMessages);
        }

        public async Task EnqueueRowCopyAsync(string sourceTableName, string destinationTableName, IEnumerable<TableEntity> entities)
        {
            await _enqueuer.EnqueueAsync(
                entities
                    .GroupBy(x => x.PartitionKey)
                    .Select(x => new TableRowCopyMessage<T>
                    {
                        SourceTableName = sourceTableName,
                        DestinationTableName = destinationTableName,
                        PartitionKey = x.Key,
                        RowKeys = x.Select(x => x.RowKey).ToList(),
                    })
                    .ToList(),
                split: m =>
                {
                    if (m.RowKeys.Count <= 2)
                    {
                        return null;
                    }

                    var firstHalf = m.RowKeys.Take(m.RowKeys.Count / 2).ToList();
                    var secondHalf = m.RowKeys.Skip(firstHalf.Count).ToList();

                    return new[]
                    {
                        new TableRowCopyMessage<T>
                        {
                            SourceTableName = m.SourceTableName,
                            DestinationTableName = m.DestinationTableName,
                            PartitionKey = m.PartitionKey,
                            RowKeys = firstHalf,
                        },
                        new TableRowCopyMessage<T>
                        {
                            SourceTableName = m.SourceTableName,
                            DestinationTableName = m.DestinationTableName,
                            PartitionKey = m.PartitionKey,
                            RowKeys = secondHalf,
                        },
                    };
                });
        }

        private TableCopyMessage<T> GetPrefixScanMessage<TParameters>(string sourceTableName, string destinationTableName, TParameters parameters)
        {
            return new TableCopyMessage<T>
            {
                SourceTableName = sourceTableName,
                DestinationTableName = destinationTableName,
                Strategy = TableCopyStrategy.PrefixScan,
                Parameters = _serializer.Serialize(parameters).AsJToken(),
            };
        }
    }
}
