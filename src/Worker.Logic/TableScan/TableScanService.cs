// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.Worker.CopyBucketRange;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.LoadBucketedPackage;
using NuGet.Insights.Worker.TableCopy;

namespace NuGet.Insights.Worker
{
    public class TableScanService
    {
        private readonly IMessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public TableScanService(
            IMessageEnqueuer enqueuer,
            SchemaSerializer serializer,
            TaskStateStorageService taskStateStorageService,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _enqueuer = enqueuer;
            _serializer = serializer;
            _taskStateStorageService = taskStateStorageService;
            _options = options;
        }

        public async Task StartEnqueueCatalogLeafScansAsync(
            TaskStateKey taskStateKey,
            string tableName,
            bool oneMessagePerId)
        {
            var taskState = await _taskStateStorageService.GetAsync(taskStateKey);

            await StartTableScanAsync<CatalogLeafScan>(
                taskState,
                TableScanDriverType.EnqueueCatalogLeafScans,
                tableName,
                TableScanStrategy.PrefixScan,
                expandPartitionKeys: true,
                partitionKeyPrefix: string.Empty,
                partitionKeyLowerBound: null,
                partitionKeyUpperBound: null,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1,
                _serializer.Serialize(new EnqueueCatalogLeafScansParameters
                {
                    OneMessagePerId = oneMessagePerId,
                }).AsJsonElement());
        }

        public async Task StartCopyBucketRangeAsync(
            TaskState taskState,
            int minBucketIndex,
            int maxBucketIndex,
            CatalogScanDriverType driverType,
            string scanId)
        {
            var partitionKeyLowerBound = minBucketIndex > 0 ? BucketedPackage.GetBucketString(minBucketIndex - 1) : null;
            var partitionKeyUpperBound = maxBucketIndex < BucketedPackage.BucketCount - 1 ? BucketedPackage.GetBucketString(maxBucketIndex + 1) : null;

            await StartTableScanAsync<BucketedPackage>(
                taskState,
                TableScanDriverType.CopyBucketRange,
                _options.Value.BucketedPackageTableName,
                TableScanStrategy.PrefixScan,
                expandPartitionKeys: true,
                partitionKeyPrefix: string.Empty,
                partitionKeyLowerBound: partitionKeyLowerBound,
                partitionKeyUpperBound: partitionKeyUpperBound,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1,
                _serializer.Serialize(new CopyBucketRangeParameters
                {
                    DriverType = driverType,
                    ScanId = scanId,
                }).AsJsonElement());
        }

        public async Task StartTableCopyAsync<T>(
            TaskState taskState,
            string sourceTable,
            string destinationTable,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound,
            TableScanStrategy strategy,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
            where T : class, ITableEntity, new()
        {
            await StartTableScanAsync<T>(
                taskState,
                TableScanDriverType.TableCopy,
                sourceTable,
                strategy,
                expandPartitionKeys: true,
                partitionKeyPrefix,
                partitionKeyLowerBound,
                partitionKeyUpperBound,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix,
                _serializer.Serialize(new TableCopyParameters
                {
                    DestinationTableName = destinationTable,
                }).AsJsonElement());
        }

        public async Task<TaskState> InitializeTaskStateAsync(TaskStateKey taskStateKey)
        {
            await _taskStateStorageService.InitializeAsync(taskStateKey.StorageSuffix);
            return await _taskStateStorageService.GetOrAddAsync(taskStateKey);
        }

        public async Task<TaskState> InitializeTaskStateAsync(TaskState taskState)
        {
            await _taskStateStorageService.InitializeAsync(taskState.StorageSuffix);
            return await _taskStateStorageService.GetOrAddAsync(taskState);
        }

        public async Task<List<TaskState>> InitializeTaskStatesAsync(string storageSuffix, string partitionKey, IReadOnlyList<string> rowKeys)
        {
            await _taskStateStorageService.InitializeAsync(storageSuffix);
            return await _taskStateStorageService.AddAsync(
                storageSuffix,
                partitionKey,
                rowKeys.Select(r => new TaskState(storageSuffix, partitionKey, r)).ToList());
        }

        public async Task DeleteTaskStateTableAsync(string storageSuffix)
        {
            await _taskStateStorageService.DeleteTableAsync(storageSuffix);
        }

        public async Task<bool> IsCompleteAsync(string storageSuffix, string partitionKey)
        {
            var taskStateCountLowerBound = await _taskStateStorageService.GetCountLowerBoundAsync(storageSuffix, partitionKey);
            return taskStateCountLowerBound == 0;
        }

        public async Task<IReadOnlyList<TaskState>> GetTaskStatesAsync(string storageSuffix, string partitionKey, string rowKeyPrefix)
        {
            return await _taskStateStorageService.GetByRowKeyPrefixAsync(storageSuffix, partitionKey, rowKeyPrefix);
        }

        private async Task StartTableScanAsync<T>(
            TaskState taskState,
            TableScanDriverType driverType,
            string sourceTable,
            TableScanStrategy strategy,
            bool expandPartitionKeys,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix,
            JsonElement driverParameters)
            where T : class, ITableEntity, new()
        {
            JsonElement? scanParameters;
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
                    }).AsJsonElement();
                    break;
                default:
                    throw new NotImplementedException();
            }

            var taskStateKey = taskState.GetKey();
            var message = new TableScanMessage<T>
            {
                Started = DateTimeOffset.UtcNow,
                TaskStateKey = taskStateKey,
                TableName = sourceTable,
                Strategy = strategy,
                DriverType = driverType,
                TakeCount = _options.Value.TableScanTakeCount,
                ExpandPartitionKeys = expandPartitionKeys,
                PartitionKeyPrefix = partitionKeyPrefix,
                PartitionKeyLowerBound = partitionKeyLowerBound,
                PartitionKeyUpperBound = partitionKeyUpperBound,
                ScanParameters = scanParameters,
                DriverParameters = driverParameters,
            };

            await _enqueuer.EnqueueAsync(new[] { message });

            taskState.Message = _serializer.Serialize(message).ToString();
            await _taskStateStorageService.UpdateAsync(taskState);
        }
    }
}
