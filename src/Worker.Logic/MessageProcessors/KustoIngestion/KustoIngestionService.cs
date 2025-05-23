// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using NuGet.Insights.Kusto;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionService
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly CsvRecordContainers _csvRecordContainers;
        private readonly CachingKustoClientFactory _kustoClientFactory;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoIngestionService> _logger;

        public KustoIngestionService(
            KustoIngestionStorageService storageService,
            IMessageEnqueuer messageEnqueuer,
            AutoRenewingStorageLeaseService leaseService,
            CsvRecordContainers csvRecordContainers,
            CachingKustoClientFactory kustoClientFactory,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoIngestionService> logger)
        {
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _leaseService = leaseService;
            _csvRecordContainers = csvRecordContainers;
            _kustoClientFactory = kustoClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _storageService.InitializeAsync(),
                _messageEnqueuer.InitializeAsync(),
                _leaseService.InitializeAsync());
        }

        public bool HasRequiredConfiguration => _options.Value.KustoConnectionString != null && _options.Value.KustoDatabaseName != null;

        public async Task<KustoIngestionEntity> StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireWithRetryAsync("Start-KustoIngestion"))
            {
                if (!lease.Acquired)
                {
                    return null;
                }

                if (await _storageService.IsIngestionRunningAsync())
                {
                    return null;
                }

                var storageId = StorageUtility.GenerateDescendingId();
                var ingestion = new KustoIngestionEntity(storageId.ToString(), storageId.Unique);

                await _messageEnqueuer.EnqueueAsync(new[]
                {
                    new KustoIngestionMessage
                    {
                        IngestionId = ingestion.IngestionId,
                    },
                });

                await _storageService.AddIngestionAsync(ingestion);
                _logger.LogInformation("Started Kusto ingestion {IngestionId}.", ingestion.IngestionId);
                return ingestion;
            }
        }

        public async Task AbortAsync()
        {
            var ingestions = await _storageService.GetIngestionsAsync();
            var latestIngestion = ingestions.MaxBy(x => x.Created);
            if (latestIngestion is null
                || latestIngestion.State == KustoIngestionState.Aborted
                || latestIngestion.State == KustoIngestionState.Complete)
            {
                return;
            }

            await _storageService.DeleteChildTableAsync(latestIngestion.StorageSuffix);

            latestIngestion.ETag = ETag.All;
            latestIngestion.State = KustoIngestionState.Aborted;
            latestIngestion.Completed = DateTimeOffset.UtcNow;
            await _storageService.ReplaceIngestionAsync(latestIngestion);
        }

        public async Task DestroyTablesAsync()
        {
            var tableNames = _csvRecordContainers
                .ContainerNames
                .SelectMany(x => new[]
                {
                    _csvRecordContainers.GetOldKustoTableName(x),
                    _csvRecordContainers.GetTempKustoTableName(x),
                    _csvRecordContainers.GetKustoTableName(x),
                })
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            await ExecuteKustoCommandAsync($".drop tables ({string.Join(", ", tableNames)}) ifexists");
        }

        private async Task ExecuteKustoCommandAsync(string command)
        {
            _logger.LogInformation("Executing Kusto command: {Command}", command);
            var adminClient = await _kustoClientFactory.GetAdminClientAsync();
            using (await adminClient.ExecuteControlCommandAsync(_options.Value.KustoDatabaseName, command))
            {
            }
        }
    }
}
