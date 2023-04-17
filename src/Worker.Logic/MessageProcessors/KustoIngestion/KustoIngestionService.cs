// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionService
    {
        private readonly KustoIngestionStorageService _storageService;
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<KustoIngestionService> _logger;

        public KustoIngestionService(
            KustoIngestionStorageService storageService,
            IMessageEnqueuer messageEnqueuer,
            AutoRenewingStorageLeaseService leaseService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<KustoIngestionService> logger)
        {
            _storageService = storageService;
            _messageEnqueuer = messageEnqueuer;
            _leaseService = leaseService;
            _options = options;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _storageService.InitializeAsync();
            await _messageEnqueuer.InitializeAsync();
            await _leaseService.InitializeAsync();
        }

        public bool HasRequiredConfiguration => _options.Value.KustoConnectionString != null && _options.Value.KustoDatabaseName != null;

        public async Task<KustoIngestionEntity> StartAsync()
        {
            await using (var lease = await _leaseService.TryAcquireAsync("Start-KustoIngestion"))
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
                        IngestionId = ingestion.GetIngestionId(),
                    },
                });

                await _storageService.AddIngestionAsync(ingestion);
                _logger.LogInformation("Started Kusto ingestion {IngestionId}.", ingestion.GetIngestionId());
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
    }
}
