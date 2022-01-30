// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Azure.Data.Tables;
using NuGet.Insights.TablePrefixScan;

namespace NuGet.Insights.Worker
{
    public class TableScanMessageProcessor<T> : IMessageProcessor<TableScanMessage<T>> where T : class, ITableEntity, new()
    {
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IMessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly TablePrefixScanner _prefixScanner;
        private readonly TableScanDriverFactory<T> _driverFactory;
        private readonly ITelemetryClient _telemetryClient;

        public TableScanMessageProcessor(
            TaskStateStorageService taskStateStorageService,
            ServiceClientFactory serviceClientFactory,
            IMessageEnqueuer enqueuer,
            SchemaSerializer serializer,
            TablePrefixScanner prefixScanner,
            TableScanDriverFactory<T> driverFactory,
            ITelemetryClient telemetryClient)
        {
            _taskStateStorageService = taskStateStorageService;
            _serviceClientFactory = serviceClientFactory;
            _enqueuer = enqueuer;
            _serializer = serializer;
            _prefixScanner = prefixScanner;
            _driverFactory = driverFactory;
            _telemetryClient = telemetryClient;
        }

        public async Task ProcessAsync(TableScanMessage<T> message, long dequeueCount)
        {
            var taskState = await _taskStateStorageService.GetAsync(message.TaskStateKey);
            if (taskState == null)
            {
                return;
            }

            switch (message.Strategy)
            {
                case TableScanStrategy.Serial:
                    await ProcessSerialAsync(message);
                    break;
                case TableScanStrategy.PrefixScan:
                    await ProcessPrefixScanAsync(message);
                    break;
                default:
                    throw new NotImplementedException();
            }

            await _taskStateStorageService.DeleteAsync(taskState);

            var sinceStarted = DateTimeOffset.UtcNow - message.Started;
            _telemetryClient
                .GetMetric("TableScanMessageProcessor.SinceStartedSeconds", "Strategy")
                .TrackValue(sinceStarted.TotalSeconds, message.Strategy.ToString());
        }

        private async Task ProcessSerialAsync(TableScanMessage<T> message)
        {
            if (message.PartitionKeyPrefix != string.Empty || !message.ExpandPartitionKeys)
            {
                throw new NotImplementedException();
            }

            using var metrics = _telemetryClient.StartQueryLoopMetrics();

            var sourceTable = await GetTableAsync(message.TableName);
            var driver = _driverFactory.Create(message.DriverType);
            await driver.InitializeAsync(message.DriverParameters);

            var pages = sourceTable.QueryAsync<T>(select: driver.SelectColumns, maxPerPage: message.TakeCount).AsPages();
            await using var enumerator = pages.GetAsyncEnumerator();
            while (await enumerator.MoveNextAsync(metrics))
            {
                await driver.ProcessEntitySegmentAsync(message.TableName, message.DriverParameters, enumerator.Current.Values);
            }
        }

        private async Task ProcessPrefixScanAsync(TableScanMessage<T> message)
        {
            var driver = _driverFactory.Create(message.DriverType);

            var tableQueryParameters = new TableQueryParameters(
                await GetTableAsync(message.TableName),
                driver.SelectColumns,
                message.TakeCount,
                message.ExpandPartitionKeys);

            int segmentsPerFirstPrefix;
            int segmentsPerSubsequentPrefix;
            TablePrefixScanStep currentStep;
            switch (_serializer.Deserialize(message.ScanParameters.Value).Data)
            {
                case TablePrefixScanStartParameters startParameters:
                    segmentsPerFirstPrefix = startParameters.SegmentsPerFirstPrefix;
                    segmentsPerSubsequentPrefix = startParameters.SegmentsPerSubsequentPrefix;
                    currentStep = new TablePrefixScanStart(
                        tableQueryParameters,
                        message.PartitionKeyPrefix);
                    break;

                case TablePrefixScanPartitionKeyQueryParameters partitionKeyQueryParameters:
                    segmentsPerFirstPrefix = partitionKeyQueryParameters.SegmentsPerFirstPrefix;
                    segmentsPerSubsequentPrefix = partitionKeyQueryParameters.SegmentsPerSubsequentPrefix;
                    currentStep = new TablePrefixScanPartitionKeyQuery(
                        tableQueryParameters,
                        partitionKeyQueryParameters.Depth,
                        partitionKeyQueryParameters.PartitionKey,
                        partitionKeyQueryParameters.RowKeySkip);
                    break;

                case TablePrefixScanPrefixQueryParameters prefixQueryParameters:
                    segmentsPerFirstPrefix = prefixQueryParameters.SegmentsPerFirstPrefix;
                    segmentsPerSubsequentPrefix = prefixQueryParameters.SegmentsPerSubsequentPrefix;
                    currentStep = new TablePrefixScanPrefixQuery(
                        tableQueryParameters,
                        prefixQueryParameters.Depth,
                        prefixQueryParameters.PartitionKeyPrefix,
                        prefixQueryParameters.PartitionKeyLowerBound);
                    break;

                default:
                    throw new NotImplementedException();
            }

            // Run as many non-async steps as possible to save needless enqueues but only perform on batch of
            // asynchronous steps to reduce runtime.
            var currentSteps = new List<TablePrefixScanStep> { currentStep };
            var enqueueSteps = new List<TablePrefixScanStep>();
            while (currentSteps.Any())
            {
                var step = currentSteps.Last();
                currentSteps.RemoveAt(currentSteps.Count - 1);
                switch (step)
                {
                    case TablePrefixScanStart start:
                        await driver.InitializeAsync(message.DriverParameters);
                        currentSteps.AddRange(_prefixScanner.Start(start));
                        break;
                    case TablePrefixScanEntitySegment<T> entitySegment:
                        enqueueSteps.Add(entitySegment);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        enqueueSteps.AddRange(await _prefixScanner.ExecutePartitionKeyQueryAsync<T>(partitionKeyQuery));
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        enqueueSteps.AddRange(await _prefixScanner.ExecutePrefixQueryAsync<T>(prefixQuery, segmentsPerFirstPrefix, segmentsPerSubsequentPrefix));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            await EnqueuePrefixScanStepsAsync(message, driver, enqueueSteps, segmentsPerFirstPrefix, segmentsPerSubsequentPrefix);
        }

        private async Task EnqueuePrefixScanStepsAsync(
            TableScanMessage<T> originalMessage,
            ITableScanDriver<T> driver,
            List<TablePrefixScanStep> nextSteps,
            int segmentsPerFirstPrefix,
            int segmentsPerSubsequentPrefix)
        {
            var entities = new List<T>();
            var tableScanMessages = new List<TableScanMessage<T>>();
            var taskStates = new List<TaskState>();

            foreach (var nextStep in nextSteps)
            {
                switch (nextStep)
                {
                    case TablePrefixScanEntitySegment<T> segment:
                        entities.AddRange(segment.Entities);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        tableScanMessages.Add(GetPrefixScanMessage(
                            originalMessage,
                            new TablePrefixScanPartitionKeyQueryParameters
                            {
                                SegmentsPerFirstPrefix = segmentsPerFirstPrefix,
                                SegmentsPerSubsequentPrefix = segmentsPerSubsequentPrefix,
                                Depth = partitionKeyQuery.Depth,
                                PartitionKey = partitionKeyQuery.PartitionKey,
                                RowKeySkip = partitionKeyQuery.RowKeySkip,
                            },
                            taskStates));
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        tableScanMessages.Add(GetPrefixScanMessage(
                            originalMessage,
                            new TablePrefixScanPrefixQueryParameters
                            {
                                SegmentsPerFirstPrefix = segmentsPerFirstPrefix,
                                SegmentsPerSubsequentPrefix = segmentsPerSubsequentPrefix,
                                Depth = prefixQuery.Depth,
                                PartitionKeyPrefix = prefixQuery.PartitionKeyPrefix,
                                PartitionKeyLowerBound = prefixQuery.PartitionKeyLowerBound,
                            },
                            taskStates));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            if (entities.Any())
            {
                await driver.ProcessEntitySegmentAsync(originalMessage.TableName, originalMessage.DriverParameters, entities);
            }

            if (tableScanMessages.Any())
            {
                await _taskStateStorageService.AddAsync(
                    originalMessage.TaskStateKey.StorageSuffix,
                    originalMessage.TaskStateKey.PartitionKey,
                    taskStates);

                await _enqueuer.EnqueueAsync(tableScanMessages);
            }
        }

        private TableScanMessage<T> GetPrefixScanMessage<TParameters>(TableScanMessage<T> originalMessage, TParameters scanParameters, List<TaskState> addedTaskStates)
        {
            var serializedParameters = _serializer.Serialize(scanParameters);

            string rowKey;
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(serializedParameters.AsString());
                rowKey = "step-" + sha256.ComputeHash(bytes).ToTrimmedBase32();
            }

            var taskState = new TaskState(
                originalMessage.TaskStateKey.StorageSuffix,
                originalMessage.TaskStateKey.PartitionKey,
                rowKey)
            {
                Parameters = serializedParameters.AsString(),
            };

            addedTaskStates.Add(taskState);

            return new TableScanMessage<T>
            {
                TaskStateKey = taskState.GetKey(),
                DriverType = originalMessage.DriverType,
                TableName = originalMessage.TableName,
                Strategy = TableScanStrategy.PrefixScan,
                TakeCount = originalMessage.TakeCount,
                ExpandPartitionKeys = originalMessage.ExpandPartitionKeys,
                PartitionKeyPrefix = originalMessage.PartitionKeyPrefix,
                ScanParameters = serializedParameters.AsJsonElement(),
                DriverParameters = originalMessage.DriverParameters,
            };
        }

        private async Task<TableClient> GetTableAsync(string name)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync()).GetTableClient(name);
        }
    }
}
