using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.TableCopy;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json.Linq;

namespace Knapcode.ExplorePackages.Worker
{
    public class TableScanService<T> where T : ITableEntity, new()
    {
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly MessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;

        public TableScanService(
            TaskStateStorageService taskStateStorageService,
            MessageEnqueuer enqueuer,
            SchemaSerializer serializer)
        {
            _taskStateStorageService = taskStateStorageService;
            _enqueuer = enqueuer;
            _serializer = serializer;
        }

        public async Task StartLatestLeafToLeafScanAsync(TaskStateKey taskStateKey, CatalogIndexScanMessage message, string tableName)
        {
            await StartTableScanAsync(
                taskStateKey,
                TableScanDriverType.LatestLeafToLeafScan,
                tableName,
                TableScanStrategy.PrefixScan,
                StorageUtility.MaxTakeCount,
                partitionKeyPrefix: string.Empty,
                _serializer.Serialize(message).AsJToken());
        }

        public async Task StartTableCopyAsync(
            TaskStateKey taskStateKey,
            string sourceTable,
            string destinationTable,
            string partitionKeyPrefix,
            TableScanStrategy strategy,
            int takeCount)
        {
            await StartTableScanAsync(
                taskStateKey,
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
            TaskStateKey taskStateKey,
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
                    TaskStateKey = taskStateKey,
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
