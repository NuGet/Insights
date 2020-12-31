using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace Knapcode.ExplorePackages.Worker.TableCopy
{
    public class TableCopyMessageProcessor<T> : IMessageProcessor<TableCopyMessage<T>> where T : ITableEntity, new()
    {
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly MessageEnqueuer _enqueuer;
        private readonly SchemaSerializer _serializer;
        private readonly TablePrefixScanner _prefixScanner;
        private readonly TableCopyEnqueuer<T> _tableCopyEnqueuer;
        private readonly ITelemetryClient _telemetryClient;

        public TableCopyMessageProcessor(
            ServiceClientFactory serviceClientFactory,
            MessageEnqueuer enqueuer,
            SchemaSerializer serializer,
            TablePrefixScanner prefixScanner,
            TableCopyEnqueuer<T> prefixScannerEnqueuer,
            ITelemetryClient telemetryClient)
        {
            _serviceClientFactory = serviceClientFactory;
            _enqueuer = enqueuer;
            _serializer = serializer;
            _prefixScanner = prefixScanner;
            _tableCopyEnqueuer = prefixScannerEnqueuer;
            _telemetryClient = telemetryClient;
        }

        public async Task ProcessAsync(TableCopyMessage<T> message, int dequeueCount)
        {
            switch (message.Strategy)
            {
                case TableCopyStrategy.Serial:
                    await ProcessSerialAsync(message);
                    break;
                case TableCopyStrategy.PrefixScan:
                    await ProcessPrefixScanAsync(message);
                    break;
            }
        }

        private async Task ProcessPrefixScanAsync(TableCopyMessage<T> message)
        {
            TablePrefixScanStep currentStep;
            switch (_serializer.Deserialize(message.Parameters))
            {
                case TablePrefixScanStartParameters startParameters:
                    currentStep = new TablePrefixScanStart(
                        MakeParameters(message, startParameters),
                        startParameters.PartitionKeyPrefix);
                    break;

                case TablePrefixScanPartitionKeyQueryParameters partitionKeyQueryParameters:
                    currentStep = new TablePrefixScanPartitionKeyQuery(
                        MakeParameters(message, partitionKeyQueryParameters),
                        partitionKeyQueryParameters.Depth,
                        partitionKeyQueryParameters.PartitionKey,
                        partitionKeyQueryParameters.RowKeySkip);
                    break;

                case TablePrefixScanPrefixQueryParameters prefixQueryParameters:
                    currentStep = new TablePrefixScanPrefixQuery(
                        MakeParameters(message, prefixQueryParameters),
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
                        await CreateDestinationTableAsync(message);
                        currentSteps.AddRange(_prefixScanner.Start(start));
                        break;
                    case TablePrefixScanEntitySegment<T> entitySegment:
                        enqueueSteps.Add(entitySegment);
                        break;
                    case TablePrefixScanPartitionKeyQuery partitionKeyQuery:
                        enqueueSteps.AddRange(await _prefixScanner.ExecutePartitionKeyQueryAsync<TableEntity>(partitionKeyQuery));
                        break;
                    case TablePrefixScanPrefixQuery prefixQuery:
                        enqueueSteps.AddRange(await _prefixScanner.ExecutePrefixQueryAsync<TableEntity>(prefixQuery));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            await _tableCopyEnqueuer.EnqueuePrefixScanStepsAsync(message.DestinationTableName, enqueueSteps);
        }

        private TableQueryParameters MakeParameters(TableCopyMessage<T> message, TablePrefixScanStepParameters parameters)
        {
            return new TableQueryParameters(
                GetTable(message.SourceTableName),
                StorageUtility.MinSelectColumns,
                parameters.TakeCount);
        }

        private async Task ProcessSerialAsync(TableCopyMessage<T> message)
        {
            using var metrics = new QueryLoopMetrics(_telemetryClient, nameof(TableCopyMessageProcessor<T>), nameof(ProcessSerialAsync));

            var sourceTable = GetTable(message.SourceTableName);
            await CreateDestinationTableAsync(message);
            var tableQuery = new TableQuery<TableEntity>
            {
                SelectColumns = StorageUtility.MinSelectColumns,
                TakeCount = StorageUtility.MaxTakeCount,
            };

            TableContinuationToken continuationToken = null;
            do
            {
                TableQuerySegment<TableEntity> segment;
                using (metrics.TrackQuery())
                {
                    segment = await sourceTable.ExecuteQuerySegmentedAsync(tableQuery, continuationToken);
                }

                await _tableCopyEnqueuer.EnqueueRowCopyAsync(message.SourceTableName, message.DestinationTableName, segment);

                continuationToken = segment.ContinuationToken;
            }
            while (continuationToken != null);
        }

        private async Task CreateDestinationTableAsync(TableCopyMessage<T> message)
        {
            await GetTable(message.DestinationTableName).CreateIfNotExistsAsync(retry: true);
        }

        private CloudTable GetTable(string name)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudTableClient()
                .GetTableReference(name);
        }
    }
}
