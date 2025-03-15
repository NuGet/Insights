// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.VerifiedPackagesToCsv
{
    public class VerifiedPackagesToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<VerifiedPackage>, VerifiedPackageRecord>
    {
        private readonly VerifiedPackagesClient _client;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public VerifiedPackagesToCsvUpdater(
            VerifiedPackagesClient client,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _client = client;
            _options = options;
        }

        public string OperationName => "VerifiedPackagesToCsv";
        public string Title => CatalogScanDriverMetadata.HumanizeCodeName(OperationName);
        public string ContainerName => _options.Value.VerifiedPackageContainerName;
        public TimeSpan Frequency => _options.Value.VerifiedPackagesToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.VerifiedPackagesV1Urls is not null && _options.Value.VerifiedPackagesV1Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartVerifiedPackagesToCsv;

        public async Task<AsOfData<VerifiedPackage>> GetDataAsync()
        {
            return await _client.GetAsync();
        }

        public async IAsyncEnumerable<IReadOnlyList<VerifiedPackageRecord>> ProduceRecordsAsync(IVersionSet versionSet, AsOfData<VerifiedPackage> data)
        {
            var verifiedPackageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await foreach (IReadOnlyList<VerifiedPackage> page in data.Pages)
            {
                foreach (VerifiedPackage entry in page)
                {
                    var id = entry.Id;
                    if (!versionSet.TryGetId(entry.Id, out id))
                    {
                        continue;
                    }

                    verifiedPackageIds.Add(id);
                }
            }

            const int pageSize = AsOfData<VerifiedPackageRecord>.DefaultPageSize;
            var outputPage = new List<VerifiedPackageRecord>(capacity: pageSize);

            foreach (var packageId in verifiedPackageIds)
            {
                outputPage.Add(new VerifiedPackageRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    Id = packageId,
                    LowerId = packageId.ToLowerInvariant(),
                    IsVerified = true,
                });
                if (outputPage.Count >= pageSize)
                {
                    yield return outputPage;
                    outputPage.Clear();
                }
            }

            // Add IDs that are not mentioned in the data and therefore are not verified. This makes joins on the
            // produced data set easier.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                outputPage.Add(new VerifiedPackageRecord
                {
                    AsOfTimestamp = data.AsOfTimestamp,
                    Id = id,
                    LowerId = id.ToLowerInvariant(),
                    IsVerified = false,
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
    }
}
