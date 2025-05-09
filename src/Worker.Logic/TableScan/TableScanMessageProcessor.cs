// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Cryptography;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.TablePrefixScan;

namespace NuGet.Insights.Worker
{
    public class TableScanMessageProcessor<T> : ITaskStateMessageProcessor<TableScanMessage<T>> where T : class, ITableEntity, new()
    {
        public const string MetricIdPrefix = $"{nameof(TableScanMessageProcessor<T>)}.";
        private static readonly string MessageTypeName = typeof(T).Name;

        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IMessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly TablePrefixScanner _prefixScanner;
        private readonly TableScanDriverFactory<T> _driverFactory;
        private readonly IOptions<NuGetInsightsSettings> _options;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<TableScanMessageProcessor<T>> _logger;
        private readonly IMetric _sinceStarted;
        private readonly IMetric _entitySegmentCount;
        private readonly IMetric _entitySegmentSize;
        private readonly IMetric _processEntities;
        private readonly IMetric _enqueuePartitionKeyQuery;
        private readonly IMetric _enqueuePrefixScanQuery;

        public TableScanMessageProcessor(
            TaskStateStorageService taskStateStorageService,
            ServiceClientFactory serviceClientFactory,
            IMessageEnqueuer enqueuer,
            SchemaSerializer serializer,
            TablePrefixScanner prefixScanner,
            TableScanDriverFactory<T> driverFactory,
            IOptions<NuGetInsightsSettings> options,
            ITelemetryClient telemetryClient,
            ILogger<TableScanMessageProcessor<T>> logger)
        {
            _taskStateStorageService = taskStateStorageService;
            _serviceClientFactory = serviceClientFactory;
            _enqueuer = enqueuer;
            _serializer = serializer;
            _prefixScanner = prefixScanner;
            _driverFactory = driverFactory;
            _options = options;
            _telemetryClient = telemetryClient;
            _logger = logger;

            _sinceStarted = _telemetryClient
                .GetMetric($"{MetricIdPrefix}SinceStartedSeconds", "Strategy", "MessageType", "DriverType");
            _entitySegmentCount = _telemetryClient
                .GetMetric($"{MetricIdPrefix}{nameof(TableScanStrategy.PrefixScan)}.EntitySegmentCount", "MessageType", "DriverType");
            _entitySegmentSize = _telemetryClient
                .GetMetric($"{MetricIdPrefix}{nameof(TableScanStrategy.PrefixScan)}.EntitySegmentSize", "MessageType", "DriverType");
            _processEntities = _telemetryClient
                .GetMetric($"{MetricIdPrefix}{nameof(TableScanStrategy.PrefixScan)}.ProcessEntitiesCount", "MessageType", "DriverType");
            _enqueuePartitionKeyQuery = _telemetryClient
                .GetMetric($"{MetricIdPrefix}{nameof(TableScanStrategy.PrefixScan)}.EnqueuePartitionKeyQueryCount", "MessageType", "DriverType");
            _enqueuePrefixScanQuery = _telemetryClient
                .GetMetric($"{MetricIdPrefix}{nameof(TableScanStrategy.PrefixScan)}.EnqueuePrefixScanQueryCount", "MessageType", "DriverType");
        }

        public async Task<TaskStateProcessResult> ProcessAsync(TableScanMessage<T> message, TaskState taskState, long dequeueCount)
        {
            if (taskState.Message is null)
            {
                return dequeueCount == 1 ? TaskStateProcessResult.Continue : TaskStateProcessResult.Delay;
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

            var strategy = message.Strategy.ToString();
            var driverType = message.DriverType.ToString();
            var sinceStarted = DateTimeOffset.UtcNow - message.Started;
            _sinceStarted.TrackValue(sinceStarted.TotalSeconds, strategy, MessageTypeName, driverType);

            return TaskStateProcessResult.Complete;
        }

        private async Task ProcessSerialAsync(TableScanMessage<T> message)
        {
            if (message.PartitionKeyPrefix != string.Empty
                || message.PartitionKeyLowerBound is not null
                || message.PartitionKeyUpperBound is not null
                || !message.ExpandPartitionKeys)
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
                        message.PartitionKeyPrefix,
                        message.PartitionKeyLowerBound,
                        message.PartitionKeyUpperBound);
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
                        prefixQueryParameters.PartitionKeyLowerBound,
                        prefixQueryParameters.PartitionKeyUpperBound);
                    break;

                default:
                    throw new NotImplementedException();
            }

            // Run as many non-async steps as possible to save needless enqueues but only perform one batch of
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
            var messageType = typeof(T).Name;
            var driverType = originalMessage.DriverType.ToString();

            var entitySegmentCount = 0;
            var enqueuePartitionKeyQuery = 0;
            var enqueuePrefixScanQuery = 0;

            foreach (var nextStep in nextSteps)
            {
                switch (nextStep)
                {
                    case TablePrefixScanEntitySegment<T> segment:
                        entitySegmentCount++;
                        _entitySegmentSize.TrackValue(segment.Entities.Count, messageType, driverType);
                        entities.AddRange(segment.Entities);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        enqueuePartitionKeyQuery++;
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
                        enqueuePrefixScanQuery++;
                        tableScanMessages.Add(GetPrefixScanMessage(
                            originalMessage,
                            new TablePrefixScanPrefixQueryParameters
                            {
                                SegmentsPerFirstPrefix = segmentsPerFirstPrefix,
                                SegmentsPerSubsequentPrefix = segmentsPerSubsequentPrefix,
                                Depth = prefixQuery.Depth,
                                PartitionKeyPrefix = prefixQuery.PartitionKeyPrefix,
                                PartitionKeyLowerBound = prefixQuery.PartitionKeyLowerBound,
                                PartitionKeyUpperBound = prefixQuery.PartitionKeyUpperBound,
                            },
                            taskStates));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            _entitySegmentCount.TrackValue(entitySegmentCount, messageType, driverType);
            _processEntities.TrackValue(entities.Count, messageType, driverType);
            _enqueuePartitionKeyQuery.TrackValue(enqueuePartitionKeyQuery, messageType, driverType);
            _enqueuePrefixScanQuery.TrackValue(enqueuePrefixScanQuery, messageType, driverType);

            if (entities.Any())
            {
                await driver.ProcessEntitySegmentAsync(originalMessage.TableName, originalMessage.DriverParameters, entities);
            }

            if (tableScanMessages.Any())
            {
                await _enqueuer.EnqueueAsync(tableScanMessages);

                await _taskStateStorageService.GetOrAddAsync(
                    originalMessage.TaskStateKey.StorageSuffix,
                    originalMessage.TaskStateKey.PartitionKey,
                    taskStates);
            }
        }

        private TableScanMessage<T> GetPrefixScanMessage<TParameters>(TableScanMessage<T> originalMessage, TParameters scanParameters, List<TaskState> addedTaskStates)
        {
            var serializedscanParameters = _serializer.Serialize(scanParameters);

            string rowKey;
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(serializedscanParameters.AsString());
                rowKey = "step-" + sha256.ComputeHash(bytes).ToTrimmedBase32();
            }

            var taskState = new TaskState(
                originalMessage.TaskStateKey.StorageSuffix,
                originalMessage.TaskStateKey.PartitionKey,
                rowKey);

            var message = new TableScanMessage<T>
            {
                Started = originalMessage.Started,
                TaskStateKey = taskState.GetKey(),
                DriverType = originalMessage.DriverType,
                TableName = originalMessage.TableName,
                Strategy = TableScanStrategy.PrefixScan,
                TakeCount = originalMessage.TakeCount,
                ExpandPartitionKeys = originalMessage.ExpandPartitionKeys,
                PartitionKeyPrefix = originalMessage.PartitionKeyPrefix,
                PartitionKeyLowerBound = originalMessage.PartitionKeyLowerBound,
                PartitionKeyUpperBound = originalMessage.PartitionKeyUpperBound,
                ScanParameters = serializedscanParameters.AsJsonElement(),
                DriverParameters = originalMessage.DriverParameters,
            };

            taskState.Message = _serializer.Serialize(message).AsString();

            addedTaskStates.Add(taskState);

            return message;
        }

        private async Task<TableClientWithRetryContext> GetTableAsync(string name)
        {
            return (await _serviceClientFactory.GetTableServiceClientAsync(_options.Value)).GetTableClient(name);
        }
    }
}
