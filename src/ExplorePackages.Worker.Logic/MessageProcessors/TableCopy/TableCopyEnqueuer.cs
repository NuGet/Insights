using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyEnqueuer<T> where T : ITableEntity, new()
    {
        private readonly MessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;

        public TableCopyEnqueuer(
            MessageEnqueuer enqueuer,
            SchemaSerializer serializer)
        {
            _enqueuer = enqueuer;
            _serializer = serializer;
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

            var tableRowCopyMessages = new List<TableRowCopyMessage<T>>();
            var tableCopyMessages = new List<TableCopyMessage<T>>();

            foreach (var nextStep in nextSteps)
            {
                switch (nextStep)
                {
                    case TablePrefixScanEntitySegment<TableEntity> segment:
                        tableRowCopyMessages.AddRange(segment
                            .Entities
                            .Select(x => new TableRowCopyMessage<T>
                            {
                                SourceTableName = segment.Parameters.Table.Name,
                                DestinationTableName = destinationTableName,
                                PartitionKey = x.PartitionKey,
                                RowKey = x.RowKey,
                            }));
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

            await _enqueuer.EnqueueAsync(tableRowCopyMessages);
            await _enqueuer.EnqueueAsync(tableCopyMessages);
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
