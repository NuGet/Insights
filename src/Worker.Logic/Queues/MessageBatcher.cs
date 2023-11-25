// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class MessageBatcher : IMessageBatcher
    {
        private readonly CatalogScanStorageService _catalogScanStorageService;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<MessageBatcher> _logger;

        public MessageBatcher(
            CatalogScanStorageService catalogScanStorageService,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<MessageBatcher> logger)
        {
            _catalogScanStorageService = catalogScanStorageService;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<HomogeneousBatchMessage>> BatchOrNullAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer)
        {
            var messageType = typeof(T);
            if (messageType == typeof(HomogeneousBatchMessage)
                || messages.Count <= 1
                || !_options.Value.AllowBatching)
            {
                return null;
            }

            var batchSize = 1;
            switch (messages.First())
            {
                case CatalogLeafScanMessage clsm:
                    var catalogLeafScan = await _catalogScanStorageService.GetLeafScanAsync(
                        clsm.StorageSuffix,
                        clsm.ScanId,
                        clsm.PageId,
                        clsm.LeafId);

                    if (catalogLeafScan is null)
                    {
                        return null;
                    }

#if ENABLE_NPE
                    if (catalogLeafScan.DriverType == CatalogScanDriverType.NuGetPackageExplorerToCsv)
                    {
                        batchSize = 1;
                        break;
                    }
#endif
                    if (catalogLeafScan.DriverType == CatalogScanDriverType.PackageAssemblyToCsv)
                    {
                        batchSize = 10;
                        break;
                    }

#if ENABLE_CRYPTOAPI
                    if (catalogLeafScan.DriverType == CatalogScanDriverType.PackageCertificateToCsv)
                    {
                        batchSize = 100;
                        break;
                    }
#endif

                    batchSize = 30;
                    break;
            }

            if (batchSize <= 1)
            {
                return null;
            }

            var batches = new List<HomogeneousBatchMessage>();
            foreach (var message in messages)
            {
                if (batches.Count == 0 || batches.Last().Messages.Count >= batchSize)
                {
                    batches.Add(new HomogeneousBatchMessage
                    {
                        SchemaName = serializer.Name,
                        SchemaVersion = serializer.LatestVersion,
                        Messages = new List<JsonElement>(),
                    });
                }

                batches.Last().Messages.Add(serializer.SerializeData(message).AsJsonElement());
            }

            _logger.LogInformation(
                "Batched {MessageCount} {SchemaName} messages into {BatchCount} batches of size {BatchSize}.",
                messages.Count,
                serializer.Name,
                batches.Count,
                batchSize);

            return batches;
        }
    }
}
