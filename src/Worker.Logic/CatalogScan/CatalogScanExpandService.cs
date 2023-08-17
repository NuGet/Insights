// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Worker
{
    public class CatalogScanExpandService
    {
        private readonly IMessageEnqueuer _messageEnqueuer;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<CatalogScanExpandService> _logger;

        public CatalogScanExpandService(
            IMessageEnqueuer messageEnqueuer,
            ITelemetryClient telemetryClient,
            ILogger<CatalogScanExpandService> logger)
        {
            _messageEnqueuer = messageEnqueuer;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task EnqueueLeafScansAsync(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            _logger.LogInformation("Enqueuing a scan of {LeafCount} leaves.", leafScans.Count);

            await _messageEnqueuer.EnqueueAsync(leafScans
                .Select(x => new CatalogLeafScanMessage
                {
                    StorageSuffix = x.StorageSuffix,
                    ScanId = x.ScanId,
                    PageId = x.PageId,
                    LeafId = x.LeafId,
                })
                .ToList());

            foreach (var leafScan in leafScans)
            {
                _telemetryClient.TrackMetric($"{nameof(CatalogScanExpandService)}.{nameof(EnqueueLeafScansAsync)}.{nameof(CatalogLeafScanMessage)}", 1, new Dictionary<string, string>
                {
                    { nameof(CatalogLeafScanMessage.StorageSuffix), leafScan.StorageSuffix },
                    { nameof(CatalogLeafScanMessage.ScanId), leafScan.ScanId },
                    { nameof(CatalogLeafScanMessage.PageId), leafScan.PageId },
                    { nameof(CatalogLeafScanMessage.LeafId), leafScan.LeafId },
                });
            }
        }
    }
}
