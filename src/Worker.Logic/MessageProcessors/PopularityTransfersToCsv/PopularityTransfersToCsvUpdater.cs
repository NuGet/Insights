// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.PopularityTransfersToCsv
{
    public class PopularityTransfersToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<PopularityTransfer>, PopularityTransfersRecord>
    {
        private readonly PopularityTransfersClient _popularityTransfersClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PopularityTransfersToCsvUpdater(
            PopularityTransfersClient popularityTransfersClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _popularityTransfersClient = popularityTransfersClient;
            _options = options;
        }

        public string OperationName => "PopularityTransfersToCsv";
        public string Title => CatalogScanDriverMetadata.HumanizeCodeName(OperationName);
        public string ContainerName => _options.Value.PopularityTransferContainerName;
        public TimeSpan Frequency => _options.Value.PopularityTransfersToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.PopularityTransfersV1Urls is not null && _options.Value.PopularityTransfersV1Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartPopularityTransfersToCsv;

        public async Task<AsOfData<PopularityTransfer>> GetDataAsync()
        {
            return await _popularityTransfersClient.GetAsync();
        }

        public async IAsyncEnumerable<PopularityTransfersRecord> ProduceRecordsAsync(IVersionSet versionSet, AsOfData<PopularityTransfer> data)
        {
            var idToTransferIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in data.Entries)
            {
                string id = entry.Id;
                if (!versionSet.TryGetId(entry.Id, out id))
                {
                    continue;
                }

                if (!idToTransferIds.TryGetValue(id, out var transferIds))
                {
                    // Only write when we move to the next ID. This ensures all of the popularity transfers from a given ID are in the same record.
                    if (idToTransferIds.Any())
                    {
                        foreach (var inner in WriteAndClear(data.AsOfTimestamp, idToTransferIds))
                        {
                            yield return inner;
                        }
                    }

                    transferIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    idToTransferIds.Add(id, transferIds);
                }

                transferIds.Add(entry.TransferId);
            }

            if (idToTransferIds.Any())
            {
                foreach (var inner in WriteAndClear(data.AsOfTimestamp, idToTransferIds))
                {
                    yield return inner;
                }
            }

            // Add IDs that are not mentioned in the data and therefore have no popularity transfers. This makes joins on the
            // produced data set easier.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                yield return new PopularityTransfersRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    Id = id,
                    LowerId = id.ToLowerInvariant(),
                    TransferIds = "[]",
                    TransferLowerIds = "[]",
                };
            }
        }

        private static IEnumerable<PopularityTransfersRecord> WriteAndClear(DateTimeOffset asOfTimestamp, Dictionary<string, HashSet<string>> fromIdToToIds)
        {
            foreach (var pair in fromIdToToIds)
            {
                var transferIds = pair.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                var transferLowerIds = transferIds.Select(x => x.ToLowerInvariant()).ToList();
                yield return new PopularityTransfersRecord
                {
                    AsOfTimestamp = asOfTimestamp,
                    Id = pair.Key,
                    LowerId = pair.Key.ToLowerInvariant(),
                    TransferIds = KustoDynamicSerializer.Serialize(transferIds),
                    TransferLowerIds = KustoDynamicSerializer.Serialize(transferLowerIds),
                };
            }

            fromIdToToIds.Clear();
        }
    }
}
