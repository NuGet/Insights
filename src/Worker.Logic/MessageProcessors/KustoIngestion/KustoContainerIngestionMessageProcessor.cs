// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Kusto;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoContainerIngestionMessageProcessor : IMessageProcessor<KustoContainerIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly CachingKustoClientFactory _kustoClientFactory;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly FanOutRecoveryService _fanOutRecoveryService;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoContainerIngestionMessageProcessor> _logger;

        public KustoContainerIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            AutoRenewingStorageLeaseService leaseService,
            CsvRecordContainers csvRecordContainers,
            CachingKustoClientFactory kustoClientFactory,
            IMessageEnqueuer messageEnqueuer,
            FanOutRecoveryService fanOutRecoveryService,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoContainerIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _leaseService = leaseService;
            _csvRecordContainers = csvRecordContainers;
            _kustoClientFactory = kustoClientFactory;
            _messageEnqueuer = messageEnqueuer;
            _fanOutRecoveryService = fanOutRecoveryService;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoContainerIngestionMessage message, long dequeueCount)
        {
            var container = await _storageService.GetContainerAsync(message.StorageSuffix, message.ContainerName);
            if (container == null)
            {
                _logger.LogTransientWarning("No matching Kusto container ingestion was found.");
                return;
            }

            var finalTableName = _csvRecordContainers.GetKustoTableName(container.ContainerName);
            if (container.State == KustoContainerIngestionState.Created)
            {
                var blobs = await _csvRecordContainers.GetBlobsAsync(container.ContainerName);
                if (blobs.Count == 0)
                {
                    _logger.LogInformation("Container {ContainerName} has no blobs so no import will occur.", container.ContainerName);
                    await _storageService.DeleteContainerAsync(container);
                    return;
                }

                container.State = KustoContainerIngestionState.CreatingTable;
                container.Started = DateTimeOffset.UtcNow;
                await _storageService.ReplaceContainerAsync(container);
            }

            var tempTableName = _csvRecordContainers.GetTempKustoTableName(container.ContainerName);
            if (container.State == KustoContainerIngestionState.CreatingTable)
            {
                await using var lease = await LeaseOrNullAsync(message, container, finalTableName);
                if (lease == null)
                {
                    return;
                }

                var containerName = container.ContainerName;

                foreach (var commandTemplate in GetDDL(containerName))
                {
                    var command = FormatCommand(containerName, tempTableName, commandTemplate);
                    await ExecuteKustoCommandAsync(container, command);
                }

                if (_options.Value.KustoApplyPartitioningPolicy)
                {
                    var commandTemplate = GetPartitioningStrategy(containerName);
                    var command = FormatCommand(containerName, tempTableName, commandTemplate);
                    await ExecuteKustoCommandAsync(container, command);
                }

                container.State = KustoContainerIngestionState.Expanding;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Expanding)
            {
                var blobs = await _csvRecordContainers.GetBlobsAsync(container.ContainerName);
                var nameToEntity = new Dictionary<string, KustoBlobIngestion>();
                foreach (var blob in blobs)
                {
                    nameToEntity.Add(blob.Name, new KustoBlobIngestion(container.ContainerName, blob.Name)
                    {
                        IngestionId = container.IngestionId,
                        StorageSuffix = container.StorageSuffix,
                        RawSizeBytes = blob.RawSizeBytes,
                        RecordCount = blob.RecordCount,
                        SourceId = Guid.NewGuid(),
                        State = KustoBlobIngestionState.Created,
                    });
                }

                await _storageService.AddBlobsAsync(container, nameToEntity.Values.ToList());

                container.State = KustoContainerIngestionState.Enqueueing;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Enqueueing)
            {
                var blobs = await _storageService.GetBlobsAsync(container);
                await EnqueueAsync(blobs);

                container.State = KustoContainerIngestionState.Working;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Requeueing)
            {
                await _fanOutRecoveryService.EnqueueUnstartedWorkAsync(
                    take => _storageService.GetUnstartedBlobsAsync(container, take),
                    EnqueueAsync,
                    metricStepName: $"{nameof(KustoContainerIngestion)}.Blob");

                container.State = KustoContainerIngestionState.Working;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Working)
            {
                var blobs = await _storageService.GetBlobsAsync(container);
                var createdCount = blobs.Count(x => x.State == KustoBlobIngestionState.Created);
                var pendingCount = blobs.Count(x => x.State == KustoBlobIngestionState.Created || x.State == KustoBlobIngestionState.Working);
                var failedCount = blobs.Count - pendingCount; // Successfully completed records are deleted so a row is either incomplete or it failed.
                if (pendingCount > 0)
                {
                    if (createdCount > 0
                        && await _fanOutRecoveryService.ShouldRequeueAsync(container.Timestamp.Value, typeof(KustoBlobIngestionMessage)))
                    {
                        container.State = KustoContainerIngestionState.Requeueing;
                        message.AttemptCount = 0;
                        await _storageService.ReplaceContainerAsync(container);
                    }

                    _logger.LogInformation(
                        "There are {Count} blobs in container {ContainerName} still being ingested into Kusto.",
                        pendingCount,
                        container.ContainerName);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else if (failedCount > 0)
                {
                    var stateSummary = blobs
                        .GroupBy(x => x.State)
                        .OrderBy(x => x.Key.ToString(), StringComparer.Ordinal)
                        .Select(x => $"{x.Key} ({x.Count()}x)")
                        .ToList();

                    _telemetryClient.TrackMetric(
                        nameof(KustoContainerIngestionMessageProcessor) + ".Failed.ElapsedMs",
                        (DateTimeOffset.UtcNow - container.Started.Value).TotalMilliseconds,
                        new Dictionary<string, string> { { "ContainerName", container.ContainerName } });

                    _logger.LogWarning(
                        "There are {Count} blobs in container {ContainerName} that were not ingested properly. The states were: {StateSummary}",
                        failedCount,
                        container.ContainerName,
                        stateSummary);

                    container.State = KustoContainerIngestionState.Failed;
                    await _storageService.ReplaceContainerAsync(container);
                }
                else
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoContainerIngestionMessageProcessor) + ".Complete.ElapsedMs",
                        (DateTimeOffset.UtcNow - container.Started.Value).TotalMilliseconds,
                        new Dictionary<string, string> { { "ContainerName", container.ContainerName } });

                    container.State = KustoContainerIngestionState.Complete;
                    await _storageService.ReplaceContainerAsync(container);
                }
            }
        }

        private async Task EnqueueAsync(IReadOnlyList<KustoBlobIngestion> blobs)
        {
            await _messageEnqueuer.EnqueueAsync(blobs.Select(x => new KustoBlobIngestionMessage
            {
                StorageSuffix = x.StorageSuffix,
                ContainerName = x.ContainerName,
                BlobName = x.BlobName,
            }).ToList());
        }

        private async Task<IAsyncDisposable> LeaseOrNullAsync(KustoContainerIngestionMessage message, KustoContainerIngestion container, string finalTableName)
        {
            // Lease on the Kusto table name to avoid weird concurrency issues.
            var lease = await _leaseService.TryAcquireAsync($"KustoContainerIngestion-{finalTableName}");
            if (!lease.Acquired)
            {
                _logger.LogTransientWarning("Container {ContainerName} lease is not available.", container.ContainerName);
                message.AttemptCount++;
                await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                return null;
            }

            return lease;
        }

        private async Task ExecuteKustoCommandAsync(KustoContainerIngestion container, string command)
        {
            _logger.LogInformation("Executing Kusto command for container {ContainerName}: {Command}", container.ContainerName, command);
            var adminClient = await _kustoClientFactory.GetAdminClientAsync();
            using (await adminClient.ExecuteControlCommandAsync(_options.Value.KustoDatabaseName, command))
            {
            }
        }

        private IReadOnlyList<string> GetDDL(string containerName)
        {
            var recordType = _csvRecordContainers.GetRecordType(containerName);
            return NuGetInsightsWorkerLogicKustoDDL.TypeToDDL[recordType];
        }

        private string GetPartitioningStrategy(string containerName)
        {
            var recordType = _csvRecordContainers.GetRecordType(containerName);
            return NuGetInsightsWorkerLogicKustoDDL.TypeToPartitioningPolicy[recordType];
        }

        private string FormatCommand(string containerName, string tableName, string commandTemplate)
        {
            var originalTableName = _csvRecordContainers.GetDefaultKustoTableName(containerName);
            var docstring = JsonSerializer.Serialize(string.Format(CultureInfo.InvariantCulture, _options.Value.KustoTableDocstringFormat, originalTableName));
            var folder = JsonSerializer.Serialize(_options.Value.KustoTableFolder);

            return commandTemplate
                .Replace("__TABLENAME__", tableName, StringComparison.Ordinal)
                .Replace("__DOCSTRING__", docstring, StringComparison.Ordinal)
                .Replace("__FOLDER__", folder, StringComparison.Ordinal);
        }
    }
}
