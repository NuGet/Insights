// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionMessageProcessor : IMessageProcessor<KustoIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ICslAdminProvider _kustoAdminClient;
        private readonly KustoDataValidator _kustoDataValidator;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoIngestionMessageProcessor> _logger;

        public KustoIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            CsvRecordContainers csvRecordContainers,
            IMessageEnqueuer messageEnqueuer,
            ICslAdminProvider kustoAdminClient,
            KustoDataValidator kustoDataValidator,
            ITelemetryClient telemetryClient,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _csvRecordContainers = csvRecordContainers;
            _messageEnqueuer = messageEnqueuer;
            _kustoAdminClient = kustoAdminClient;
            _kustoDataValidator = kustoDataValidator;
            _telemetryClient = telemetryClient;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoIngestionMessage message, long dequeueCount)
        {
            var ingestion = await _storageService.GetIngestionAsync(message.IngestionId);
            if (ingestion is null)
            {
                if (message.AttemptCount < 10)
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the Kusto ingestion {IngestionId} should have already been created. Trying again.",
                        message.AttemptCount,
                        message.IngestionId);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                }
                else
                {
                    _logger.LogTransientWarning("After {AttemptCount} attempts, the Kusto ingestion {IngestionId} should have already been created. Giving up.",
                        message.AttemptCount,
                        message.IngestionId);
                }

                return;
            }

            if (ingestion.State == KustoIngestionState.Created)
            {
                _logger.LogInformation("The Kusto ingestion is starting.");

                ingestion.Started = DateTimeOffset.UtcNow;
                ingestion.State = KustoIngestionState.Expanding;
                ingestion.AttemptCount = 1;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Expanding)
            {
                await _storageService.InitializeChildTableAsync(ingestion.StorageSuffix);

                await _storageService.AddContainersAsync(ingestion, _csvRecordContainers.ContainerNames);

                ingestion.State = KustoIngestionState.Enqueuing;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Retrying)
            {
                var containers = await _storageService.GetContainersAsync(ingestion);

                // Move the failed containers to the retrying state. This allows us to delete the blob records without
                // having the containers accidentally move to the completed state when all blob records are gone.
                var failedContainers = containers.Where(x => x.State == KustoContainerIngestionState.Failed).ToList();
                if (failedContainers.Count > 0)
                {
                    foreach (var container in failedContainers)
                    {
                        container.State = KustoContainerIngestionState.Retrying;
                    }
                    await _storageService.ReplaceContainersAsync(failedContainers);
                }

                // Move the retrying containers to the created state after cleaning up any blob records  (i.e. the
                // failed/timed out ones that caused the retry)
                var retryingContainers = containers.Where(x => x.State == KustoContainerIngestionState.Retrying).ToList();
                foreach (var container in retryingContainers)
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoIngestionMessageProcessor) + ".RetryingContainer.ElapsedMs",
                        (DateTimeOffset.UtcNow - container.Started.Value).TotalMilliseconds,
                        new Dictionary<string, string> { { "ContainerName", container.ContainerName } });

                    container.State = KustoContainerIngestionState.Created;

                    var blobs = await _storageService.GetBlobsAsync(container);
                    await _storageService.DeleteBlobsAsync(blobs);
                }
                await _storageService.ReplaceContainersAsync(retryingContainers);

                ingestion.State = KustoIngestionState.Enqueuing;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Enqueuing)
            {
                var containers = await _storageService.GetContainersAsync(ingestion);
                var createdContainers = containers.Where(x => x.State == KustoContainerIngestionState.Created).ToList();
                await _messageEnqueuer.EnqueueAsync(createdContainers.Select(x => new KustoContainerIngestionMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ContainerName = x.ContainerName,
                }).ToList());

                ingestion.State = KustoIngestionState.Working;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Working)
            {
                var containers = await _storageService.GetContainersAsync(ingestion);
                var incompleteCount = containers.Count(x => x.State != KustoContainerIngestionState.Complete && x.State != KustoContainerIngestionState.Failed);
                var errorCount = containers.Count(x => x.State == KustoContainerIngestionState.Failed);
                if (containers.Count == 0)
                {
                    ingestion.State = KustoIngestionState.Finalizing;
                    await _storageService.ReplaceIngestionAsync(ingestion);
                }
                else if (incompleteCount > 0)
                {
                    _logger.LogInformation("There are {Count} containers still being ingested into Kusto.", incompleteCount);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else if (errorCount > 0)
                {
                    _logger.LogWarning("There are {Count} containers that could not be fully ingested into Kusto.", errorCount);

                    if (ingestion.AttemptCount >= _options.Value.KustoIngestionMaxAttempts)
                    {
                        throw new InvalidOperationException($"At least one container failed to be ingested into Kusto, after {ingestion.AttemptCount} attempts.");
                    }

                    _telemetryClient.TrackMetric(
                        nameof(KustoIngestionMessageProcessor) + ".Retrying.ElapsedMs",
                        (DateTimeOffset.UtcNow - ingestion.Started.Value).TotalMilliseconds);

                    ingestion.AttemptCount++;
                    ingestion.State = KustoIngestionState.Retrying;
                    await _storageService.ReplaceIngestionAsync(ingestion);

                    // Delay the message based on the ingestion attempt count. If we're encountering recurring failures
                    // from Kusto, it's wise to slow down and try again a bit later.
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(
                        ingestion.AttemptCount - 1, // First retry should be immediate
                        factor: 60, // Delay by one more minute each attempt
                        maxSeconds: 600)); // Only wait for up to 10 minutes between attempts
                    return;
                }
                else
                {
                    ingestion.State = KustoIngestionState.Validating;
                    await _storageService.ReplaceIngestionAsync(ingestion);
                }
            }

            if (ingestion.State == KustoIngestionState.Validating)
            {
                var validationAttempts = 0;
                bool valid;
                do
                {
                    if (validationAttempts > 0)
                    {
                        _telemetryClient.TrackMetric(
                            nameof(KustoIngestionMessageProcessor) + ".RetryingValidation.ElapsedMs",
                            (DateTimeOffset.UtcNow - ingestion.Started.Value).TotalMilliseconds);

                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }

                    valid = await _kustoDataValidator.ValidateAsync();
                    validationAttempts++;
                }
                while (!valid && validationAttempts < _options.Value.KustoValidationMaxAttempts);

                if (!valid)
                {
                    _telemetryClient.TrackMetric(
                        nameof(KustoIngestionMessageProcessor) + ".FailedValidation.ElapsedMs",
                        (DateTimeOffset.UtcNow - ingestion.Started.Value).TotalMilliseconds);

                    _logger.LogWarning("The Kusto validation failed.");
                    await CleanUpAndSetTerminalStateAsync(ingestion, KustoIngestionState.FailedValidation);
                    return;
                }

                ingestion.State = KustoIngestionState.ConfirmingNewerData;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.ConfirmingNewerData)
            {
                var newer = await _kustoDataValidator.IsIngestedDataNewerAsync();
                if (newer == false)
                {
                    _logger.LogWarning("The ingested data is older than the existing data. No table swap will occur.");
                    await DropTempTablesAsync(ingestion);

                    ingestion.State = KustoIngestionState.Finalizing;
                    await _storageService.ReplaceIngestionAsync(ingestion);
                }
                else
                {
                    ingestion.State = KustoIngestionState.SwappingTables;
                    await _storageService.ReplaceIngestionAsync(ingestion);
                }
            }

            if (ingestion.State == KustoIngestionState.SwappingTables)
            {
                await DropOldTablesAsync(ingestion);
                await SwapIngestedTablesAsync(ingestion);

                ingestion.State = KustoIngestionState.DroppingOldTables;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.DroppingOldTables)
            {
                await DropOldTablesAsync(ingestion);

                ingestion.State = KustoIngestionState.Finalizing;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Finalizing)
            {
                _telemetryClient.TrackMetric(
                    nameof(KustoIngestionMessageProcessor) + ".Complete.ElapsedMs",
                    (DateTimeOffset.UtcNow - ingestion.Started.Value).TotalMilliseconds);

                _logger.LogInformation("The Kusto ingestion is complete.");
                await CleanUpAndSetTerminalStateAsync(ingestion, KustoIngestionState.Complete);
            }
        }

        private async Task CleanUpAndSetTerminalStateAsync(KustoIngestionEntity ingestion, KustoIngestionState terminalState)
        {
            await _storageService.DeleteChildTableAsync(ingestion.StorageSuffix);
            await _storageService.DeleteOldIngestionsAsync(ingestion.IngestionId);

            ingestion.Completed = DateTimeOffset.UtcNow;
            ingestion.State = terminalState;
            await _storageService.ReplaceIngestionAsync(ingestion);
        }

        private async Task SwapIngestedTablesAsync(KustoIngestionEntity ingestion)
        {
            var ingestedContainers = await _storageService.GetContainersAsync(ingestion);
            var swaps = new List<string>();
            foreach (var container in ingestedContainers)
            {
                var containerName = container.ContainerName;
                var old = _csvRecordContainers.GetOldKustoTableName(containerName);
                var final = _csvRecordContainers.GetKustoTableName(containerName);
                var temp = _csvRecordContainers.GetTempKustoTableName(containerName);

                swaps.Add($"{old} = {final} ifexists");
                swaps.Add($"{final} = {temp}");
            }

            var swapCommand = $".rename tables {string.Join(", ", swaps)}";
            await ExecuteKustoCommandAsync(swapCommand);
        }

        private async Task DropTempTablesAsync(KustoIngestionEntity ingestion)
        {
            var ingestedContainers = await _storageService.GetContainersAsync(ingestion);
            var allTemp = ingestedContainers.Select(x => _csvRecordContainers.GetTempKustoTableName(x.ContainerName));
            var dropOldCommand = $".drop tables ({string.Join(", ", allTemp)}) ifexists";
            await ExecuteKustoCommandAsync(dropOldCommand);
        }

        private async Task DropOldTablesAsync(KustoIngestionEntity ingestion)
        {
            var ingestedContainers = await _storageService.GetContainersAsync(ingestion);
            var allOld = ingestedContainers.Select(x => _csvRecordContainers.GetOldKustoTableName(x.ContainerName));
            var dropOldCommand = $".drop tables ({string.Join(", ", allOld)}) ifexists";
            await ExecuteKustoCommandAsync(dropOldCommand);
        }

        private async Task ExecuteKustoCommandAsync(string command)
        {
            _logger.LogInformation("Executing Kusto command: {Command}", command);
            using (await _kustoAdminClient.ExecuteControlCommandAsync(_options.Value.KustoDatabaseName, command))
            {
            }
        }
    }
}
