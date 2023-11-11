// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Data.Tables;
using NuGet.Insights.Worker.EnqueueCatalogLeafScan;
using NuGet.Insights.Worker.TableCopy;

namespace NuGet.Insights.Worker
{
    public class TableScanService
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
            await StartTableScanAsync<CatalogLeafScan>(
                taskStateKey,
                TableScanDriverType.EnqueueCatalogLeafScans,
                tableName,
                TableScanStrategy.PrefixScan,
                StorageUtility.MaxTakeCount,
                expandPartitionKeys: !oneMessagePerId,
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

        public async Task StartTableCopyAsync<T>(
            TaskStateKey taskStateKey,
            string sourceTable,
            string destinationTable,
            string partitionKeyPrefix,
            string partitionKeyLowerBound,
            string partitionKeyUpperBound,
            TableScanStrategy strategy,
            int takeCount,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
            where T : class, ITableEntity, new()
        {
            await StartTableScanAsync<T>(
                taskStateKey,
                TableScanDriverType.TableCopy,
                sourceTable,
                strategy,
                takeCount,
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

        private async Task StartTableScanAsync<T>(
            TaskStateKey taskStateKey,
            TableScanDriverType driverType,
            string sourceTable,
            TableScanStrategy strategy,
            int takeCount,
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
                    PartitionKeyLowerBound = partitionKeyLowerBound,
                    PartitionKeyUpperBound = partitionKeyUpperBound,
                    ScanParameters = scanParameters,
                    DriverParameters = driverParameters,
                },
            });
        }
    }
}
