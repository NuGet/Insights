using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.EnqueueCatalogLeafScan;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class TableScanService<T> where T : ITableEntity, new()
    {
        private readonly IMessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;

        public TableScanService(
            IMessageEnqueuer enqueuer,
            SchemaSerializer serializer)
        {
            _enqueuer = enqueuer;
            _serializer = serializer;
        }

        public async Task StartEnqueueCatalogLeafScansAsync(
            TaskStateKey taskStateKey,
            string tableName,
            bool oneMessagePerId)
        {
            await StartTableScanAsync(
                taskStateKey,
                TableScanDriverType.EnqueueCatalogLeafScans,
                tableName,
                TableScanStrategy.PrefixScan,
                StorageUtility.MaxTakeCount,
                expandPartitionKeys: !oneMessagePerId,
                partitionKeyPrefix: string.Empty,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1,
                _serializer.Serialize(new EnqueueCatalogLeafScansParameters
                {
                    OneMessagePerId = oneMessagePerId,
                }).AsJToken());
        }

        public async Task StartTableCopyAsync(
            TaskStateKey taskStateKey,
            string sourceTable,
            string destinationTable,
            string partitionKeyPrefix,
            TableScanStrategy strategy,
            int takeCount,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
        {
            await StartTableScanAsync(
                taskStateKey,
                TableScanDriverType.TableCopy,
                sourceTable,
                strategy,
                takeCount,
                expandPartitionKeys: true,
                partitionKeyPrefix,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix,
                _serializer.Serialize(new TableCopyParameters
                {
                    DestinationTableName = destinationTable,
                }).AsJToken());
        }

        private async Task StartTableScanAsync(
            TaskStateKey taskStateKey,
            TableScanDriverType driverType,
            string sourceTable,
            TableScanStrategy strategy,
            int takeCount,
            bool expandPartitionKeys,
            string partitionKeyPrefix,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix,
            JToken driverParameters)
        {
            JToken scanParameters;
            switch (strategy)
            {
                case TableScanStrategy.Serial:
                    scanParameters = null;
                    break;
                case TableScanStrategy.PrefixScan:
                    scanParameters = _serializer.Serialize(new TablePrefixScanStartParameters
                    {
                        SegmentsPerFirstPrefix = segmentsPerFirstPrefix,
                        SegmentsPerSubsequentPrefix = segmentsPerSubsequentPrefix,
                    }).AsJToken();
                    break;
                default:
                    throw new NotImplementedException();
            }

            await _enqueuer.EnqueueAsync(new[]
            {
                new TableScanMessage<T>
                {
                    Started = DateTimeOffset.UtcNow,
                    TaskStateKey = taskStateKey,
                    TableName = sourceTable,
                    Strategy = strategy,
                    DriverType = driverType,
                    TakeCount = takeCount,
                    ExpandPartitionKeys = expandPartitionKeys,
                    PartitionKeyPrefix = partitionKeyPrefix,
                    ScanParameters = scanParameters,
                    DriverParameters = driverParameters,
                },
            });
        }
    }
}
