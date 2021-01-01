using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class TableScanService<T> where T : ITableEntity, new()
    {
        private readonly MessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;

        public TableScanService(
            MessageEnqueuer enqueuer,
            SchemaSerializer serializer)
        {
            _enqueuer = enqueuer;
            _serializer = serializer;
        }

        public async Task StartTableCopyAsync(
            string sourceTable,
            string destinationTable,
            string partitionKeyPrefix,
            TableScanStrategy strategy,
            int takeCount)
        {
            await StartTableScanAsync(
                TableScanDriverType.TableCopy,
                sourceTable,
                strategy,
                takeCount,
                partitionKeyPrefix,
                _serializer.Serialize(new TableCopyParameters
                {
                    DestinationTableName = destinationTable,
                }).AsJToken());
        }

        private async Task StartTableScanAsync(
            TableScanDriverType driverType,
            string sourceTable,
            TableScanStrategy strategy,
            int takeCount,
            string partitionKeyPrefix,
            JToken driverParameters)
        {
            JToken scanParameters;
            switch (strategy)
            {
                case TableScanStrategy.Serial:
                    scanParameters = null;
                    break;
                case TableScanStrategy.PrefixScan:
                    scanParameters = null;
                    break;
                default:
                    throw new NotImplementedException();
            }

            await _enqueuer.EnqueueAsync(new[]
            {
                new TableScanMessage<T>
                {
                    TableName = sourceTable,
                    Strategy = strategy,
                    DriverType = driverType,
                    TakeCount = takeCount,
                    PartitionKeyPrefix = partitionKeyPrefix,
                    ScanParameters = scanParameters,
                    DriverParameters = driverParameters,
                },
            });
        }
    }
}
