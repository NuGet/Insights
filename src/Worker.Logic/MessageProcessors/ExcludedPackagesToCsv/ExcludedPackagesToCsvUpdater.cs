// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.ExcludedPackagesToCsv
{
    public class ExcludedPackagesToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<ExcludedPackage>, ExcludedPackageRecord>
    {
        private readonly ExcludedPackagesClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public ExcludedPackagesToCsvUpdater(
            ExcludedPackagesClient client,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _client = client;
            _options = options;
        }

        public string OperationName => "ExcludedPackagesToCsv";
        public string Title => CatalogScanDriverMetadata.HumanizeCodeName(OperationName);
        public string ContainerName => _options.Value.ExcludedPackageContainerName;
        public TimeSpan Frequency => _options.Value.ExcludedPackagesToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.ExcludedPackagesV1Urls is not null && _options.Value.ExcludedPackagesV1Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartVerifiedPackagesToCsv;

        public async Task<AsOfData<ExcludedPackage>> GetDataAsync()
        {
            return await _client.GetAsync();
        }

        public async IAsyncEnumerable<ExcludedPackageRecord> ProduceRecordsAsync(IVersionSet versionSet, AsOfData<ExcludedPackage> data)
        {
            var verifiedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in data.Entries)
            {
                var id = entry.Id;
                if (!versionSet.TryGetId(entry.Id, out id))
                {
                    continue;
                }

                verifiedPackageIds.Add(id);
            }

            foreach (var packageId in verifiedPackageIds)
            {
                yield return new ExcludedPackageRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    Id = packageId,
                    LowerId = packageId.ToLowerInvariant(),
                    IsExcluded = true,
                };
            }

            // Add IDs that are not mentioned in the data and therefore are not excluded. This makes joins on the
            // produced data set easier.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                yield return new ExcludedPackageRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    Id = id,
                    LowerId = id.ToLowerInvariant(),
                    IsExcluded = false,
                };
            }
        }
    }
}
