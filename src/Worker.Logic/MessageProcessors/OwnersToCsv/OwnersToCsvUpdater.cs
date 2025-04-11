// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.OwnersToCsv
{
    public class OwnersToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<PackageOwner>, PackageOwnerRecord>
    {
        private readonly PackageOwnersClient _packageOwnersClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public OwnersToCsvUpdater(
            PackageOwnersClient packageOwnersClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _packageOwnersClient = packageOwnersClient;
            _options = options;
        }

        public string OperationName => "OwnersToCsv";
        public string Title => CatalogScanDriverMetadata.HumanizeCodeName(OperationName);
        public string ContainerName => _options.Value.PackageOwnerContainerName;
        public TimerFrequency Frequency => TimerFrequency.Parse(_options.Value.OwnersToCsvFrequency);
        public bool HasRequiredConfiguration => _options.Value.OwnersV2Urls is not null && _options.Value.OwnersV2Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartOwnersToCsv;

        public async Task<AsOfData<PackageOwner>> GetDataAsync()
        {
            return await _packageOwnersClient.GetAsync();
        }

        public async IAsyncEnumerable<IReadOnlyList<PackageOwnerRecord>> ProduceRecordsAsync(IVersionSet versionSet, AsOfData<PackageOwner> data)
        {
            var idToOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            const int pageSize = AsOfData<PackageOwnerRecord>.DefaultPageSize;
            var outputPage = new List<PackageOwnerRecord>(pageSize);

            await foreach (IReadOnlyList<PackageOwner> page in data.Pages)
            {
                foreach (PackageOwner entry in page)
                {
                    string id = entry.Id;
                    if (!versionSet.TryGetId(entry.Id, out id))
                    {
                        continue;
                    }

                    if (!idToOwners.TryGetValue(id, out var owners))
                    {
                        // Only write when we move to the next ID. This ensures all of the owners of a given ID are in the same record.
                        if (idToOwners.Any())
                        {
                            foreach (var inner in WriteAndClear(data.AsOfTimestamp, idToOwners))
                            {
                                outputPage.Add(inner);
                                if (outputPage.Count >= pageSize)
                                {
                                    yield return outputPage;
                                    outputPage.Clear();
                                }
                            }
                        }

                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        idToOwners.Add(id, owners);
                    }

                    owners.Add(entry.Username);
                }
            }

            if (idToOwners.Any())
            {
                foreach (PackageOwnerRecord inner in WriteAndClear(data.AsOfTimestamp, idToOwners))
                {
                    outputPage.Add(inner);
                    if (outputPage.Count >= pageSize)
                    {
                        yield return outputPage;
                        outputPage.Clear();
                    }
                }
            }

            // Add IDs that are not mentioned in the data and therefore have no owners. This makes joins on the
            // produced data set easier.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                outputPage.Add(new PackageOwnerRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    Id = id,
                    LowerId = id.ToLowerInvariant(),
                    Owners = "[]",
                });
                if (outputPage.Count >= pageSize)
                {
                    yield return outputPage;
                    outputPage.Clear();
                }
            }

            if (outputPage.Count > 0)
            {
                yield return outputPage;
            }
        }

        private static IEnumerable<PackageOwnerRecord> WriteAndClear(DateTimeOffset asOfTimestamp, Dictionary<string, HashSet<string>> idToOwners)
        {
            foreach (var pair in idToOwners)
            {
                yield return new PackageOwnerRecord
                {
                    AsOfTimestamp = asOfTimestamp,
                    Id = pair.Key,
                    LowerId = pair.Key.ToLowerInvariant(),
                    Owners = KustoDynamicSerializer.Serialize(pair.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()),
                };
            }

            idToOwners.Clear();
        }
    }
}
