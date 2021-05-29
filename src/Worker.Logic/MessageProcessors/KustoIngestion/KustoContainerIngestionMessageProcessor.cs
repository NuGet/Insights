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
    public class KustoContainerIngestionMessageProcessor : IMessageProcessor<KustoContainerIngestionMessage>
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CsvResultStorageContainers _csvRecordContainers;
        private readonly ICslAdminProvider _kustoAdminClient;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoContainerIngestionMessageProcessor> _logger;

        public KustoContainerIngestionMessageProcessor(
            KustoIngestionStorageService storageService,
            AutoRenewingStorageLeaseService leaseService,
            CsvResultStorageContainers csvRecordContainers,
            ICslAdminProvider kustoAdminClient,
            IMessageEnqueuer messageEnqueuer,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoContainerIngestionMessageProcessor> logger)
        {
            _storageService = storageService;
            _leaseService = leaseService;
            _csvRecordContainers = csvRecordContainers;
            _kustoAdminClient = kustoAdminClient;
            _messageEnqueuer = messageEnqueuer;
            _options = options;
            _logger = logger;
        }

        public async Task ProcessAsync(KustoContainerIngestionMessage message, long dequeueCount)
        {
            var container = await _storageService.GetContainerAsync(message.StorageSuffix, message.ContainerName);
            if (container == null)
            {
                _logger.LogWarning("No matching Kusto container ingestion was found.");
                return;
            }

            var finalTableName = _csvRecordContainers.GetKustoTableName(container.GetContainerName());
            if (container.State == KustoContainerIngestionState.Created)
            {
                var blobs = await _csvRecordContainers.GetBlobsAsync(container.GetContainerName());
                if (blobs.Count == 0)
                {
                    _logger.LogInformation("Container {ContainerName} has no blobs so no import will occur.", container.GetContainerName());
                    await _storageService.DeleteContainerAsync(container);
                    return;
                }

                container.State = KustoContainerIngestionState.CreatingTable;
                await _storageService.ReplaceContainerAsync(container);
            }

            var tempTableName = _csvRecordContainers.GetTempKustoTableName(container.GetContainerName());
            if (container.State == KustoContainerIngestionState.CreatingTable)
            {
                await using var lease = await LeaseOrNullAsync(message, container, finalTableName);
                if (lease == null)
                {
                    return;
                }

                foreach (var commandTemplate in GetDDL(container.GetContainerName()))
                {
                    var command = FormatCommand(tempTableName, commandTemplate);
                    await ExecuteKustoCommandAsync(container, command);
                }

                if (_options.Value.KustoApplyPartitioningPolicy)
                {
                    var commandTemplate = GetPartitioningStrategy(container.GetContainerName());
                    var command = FormatCommand(tempTableName, commandTemplate);
                    await ExecuteKustoCommandAsync(container, command);
                }

                container.State = KustoContainerIngestionState.Expanding;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Expanding)
            {
                var blobs = await _csvRecordContainers.GetBlobsAsync(container.GetContainerName());
                var nameToEntity = new Dictionary<string, KustoBlobIngestion>();
                foreach (var blob in blobs)
                {
                    var url = await _csvRecordContainers.GetBlobUrlAsync(container.GetContainerName(), blob.Name);
                    var cleanUrl = new UriBuilder(url) { Query = null };
                    nameToEntity.Add(blob.Name, new KustoBlobIngestion(container.GetContainerName(), blob.Name)
                    {
                        IngestionId = container.IngestionId,
                        StorageSuffix = container.StorageSuffix,
                        RawSizeBytes = blob.RawSizeBytes,
                        SourceId = Guid.NewGuid(),
                        SourceUrl = cleanUrl.Uri.AbsoluteUri,
                        State = KustoBlobIngestionState.Created,
                    });
                }

                await _storageService.AddBlobsAsync(container, nameToEntity.Values.ToList());

                container.State = KustoContainerIngestionState.Enqueuing;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Enqueuing)
            {
                var blobs = await _storageService.GetBlobsAsync(container);
                await _messageEnqueuer.EnqueueAsync(blobs.Select(x => new KustoBlobIngestionMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ContainerName = x.GetContainerName(),
                    BlobName = x.GetBlobName(),
                }).ToList());

                container.State = KustoContainerIngestionState.Working;
                await _storageService.ReplaceContainerAsync(container);
            }

            if (container.State == KustoContainerIngestionState.Working)
            {
                var countLowerBound = await _storageService.GetBlobCountLowerBoundAsync(container.StorageSuffix, container.GetContainerName());
                if (countLowerBound > 0)
                {
                    _logger.LogInformation(
                        "There are at least {CountLowerBound} blobs in container {ContainerName} still being ingested into Kusto.",
                        countLowerBound,
                        container.GetContainerName());
                    message.AttemptCount++;
                    await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                    return;
                }
                else
                {
                    container.State = KustoContainerIngestionState.Complete;
                    await _storageService.ReplaceContainerAsync(container);
                }
            }
        }

        private async Task<IAsyncDisposable> LeaseOrNullAsync(KustoContainerIngestionMessage message, KustoContainerIngestion container, string finalTableName)
        {
            // Lease on the Kusto table name to avoid weird concurrency issues.
            var lease = await _leaseService.TryAcquireAsync($"KustoContainerIngestion-{finalTableName}");
            if (!lease.Acquired)
            {
                _logger.LogWarning("Container {ContainerName} lease is not available.", container.GetContainerName());
                message.AttemptCount++;
                await _messageEnqueuer.EnqueueAsync(new[] { message }, StorageUtility.GetMessageDelay(message.AttemptCount));
                return null;
            }

            return lease;
        }

        private async Task ExecuteKustoCommandAsync(KustoContainerIngestion container, string command)
        {
            _logger.LogInformation("Executing Kusto command for container {ContainerName}: {Command}", container.GetContainerName(), command);
            using (await _kustoAdminClient.ExecuteControlCommandAsync(_options.Value.KustoDatabaseName, command))
            {
            }
        }

        private IReadOnlyList<string> GetDDL(string containerName)
        {
            var recordType = _csvRecordContainers.GetRecordType(containerName);
            return KustoDDL.TypeToDDL[recordType];
        }

        private string GetPartitioningStrategy(string containerName)
        {
            var recordType = _csvRecordContainers.GetRecordType(containerName);
            return KustoDDL.TypeToPartitioningPolicy[recordType];
        }

        private string FormatCommand(string tableName, string commandTemplate)
        {
            return commandTemplate.Replace("__TABLENAME__", tableName);
        }
    }
}
