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
        private readonly CsvResultStorageContainers _csvRecordContainers;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ICslAdminProvider _kustoAdminClient;
        private readonly KustoDataValidator _kustoDataValidator;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoIngestionMessageProcessor> _logger;

        public KustoIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            CsvResultStorageContainers csvRecordContainers,
            IMessageEnqueuer messageEnqueuer,
            ICslAdminProvider kustoAdminClient,
            KustoDataValidator kustoDataValidator,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _csvRecordContainers = csvRecordContainers;
            _messageEnqueuer = messageEnqueuer;
            _kustoAdminClient = kustoAdminClient;
            _kustoDataValidator = kustoDataValidator;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoIngestionMessage message, long dequeueCount)
        {
            var ingestion = await _storageService.GetIngestionAsync(message.IngestionId);
            if (ingestion == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(dequeueCount * 15));
                throw new InvalidOperationException($"An incomplete Kusto ingestion should have already been created.");
            }

            if (ingestion.State == KustoIngestionState.Created)
            {
                _logger.LogInformation("The Kusto ingestion is starting.");

                ingestion.Created = DateTimeOffset.UtcNow;
                ingestion.State = KustoIngestionState.Expanding;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Expanding)
            {
                await _storageService.InitializeChildTableAsync(ingestion.StorageSuffix);

                var allContainerNames = _csvRecordContainers.GetContainerNames();
                await _storageService.AddContainersAsync(ingestion, allContainerNames);

                ingestion.State = KustoIngestionState.Enqueuing;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Enqueuing)
            {
                var containers = await _storageService.GetContainersAsync(ingestion);
                await _messageEnqueuer.EnqueueAsync(containers.Select(x => new KustoContainerIngestionMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ContainerName = x.GetContainerName(),
                }).ToList());

                ingestion.State = KustoIngestionState.Working;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }

            if (ingestion.State == KustoIngestionState.Working)
            {
                var containers = await _storageService.GetContainersAsync(ingestion);
                int incompleteCount;
                if (containers.Count == 0)
                {
                    ingestion.State = KustoIngestionState.Finalizing;
                    await _storageService.ReplaceIngestionAsync(ingestion);
                }
                else if ((incompleteCount = containers.Count(x => x.State != KustoContainerIngestionState.Complete)) != 0)
                {
                    _logger.LogInformation("There are {Count} containers still being ingested into Kusto.", incompleteCount);
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
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
                if (dequeueCount > 5)
                {
                    throw new InvalidOperationException("The validation has failed too many times.");
                }

                await _kustoDataValidator.ValidateAsync();

                ingestion.State = KustoIngestionState.SwappingTables;
                await _storageService.ReplaceIngestionAsync(ingestion);
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
                await _storageService.DeleteChildTableAsync(ingestion.StorageSuffix);
                await _storageService.DeleteOldIngestionsAsync(ingestion.GetIngestionId());

                _logger.LogInformation("The Kusto ingestion is complete.");

                ingestion.Completed = DateTimeOffset.UtcNow;
                ingestion.State = KustoIngestionState.Complete;
                await _storageService.ReplaceIngestionAsync(ingestion);
            }
        }

        private async Task SwapIngestedTablesAsync(KustoIngestionEntity ingestion)
        {
            var ingestedContainers = await _storageService.GetContainersAsync(ingestion);
            var swaps = new List<string>();
            foreach (var container in ingestedContainers)
            {
                var containerName = container.GetContainerName();
                var old = _csvRecordContainers.GetOldKustoTableName(containerName);
                var final = _csvRecordContainers.GetKustoTableName(containerName);
                var temp = _csvRecordContainers.GetTempKustoTableName(containerName);

                swaps.Add($"{old} = {final} ifexists");
                swaps.Add($"{final} = {temp}");
            }

            var swapCommand = $".rename tables {string.Join(", ", swaps)}";
            await ExecuteKustoCommandAsync(swapCommand);
        }

        private async Task DropOldTablesAsync(KustoIngestionEntity ingestion)
        {
            var ingestedContainers = await _storageService.GetContainersAsync(ingestion);
            var allOld = ingestedContainers.Select(x => _csvRecordContainers.GetOldKustoTableName(x.GetContainerName()));
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
