// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public class DownloadsToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<PackageDownloads>, PackageDownloadRecord>
    {
        private readonly IPackageDownloadsClient _packageDownloadsClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public DownloadsToCsvUpdater(
            IPackageDownloadsClient packageDownloadsClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _packageDownloadsClient = packageDownloadsClient;
            _options = options;
        }

        public string OperationName => "DownloadsToCsv";
        public string Title => CatalogScanDriverMetadata.HumanizeCodeName(OperationName);
        public string ContainerName => _options.Value.PackageDownloadContainerName;
        public TimerFrequency Frequency => TimerFrequency.Parse(_options.Value.DownloadToCsvFrequency);
        public bool HasRequiredConfiguration => _options.Value.DownloadsV1Urls is not null && _options.Value.DownloadsV1Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartDownloadToCsv;

        public async Task<AsOfData<PackageDownloads>> GetDataAsync()
        {
            return await _packageDownloadsClient.GetAsync();
        }

        public IAsyncEnumerable<IReadOnlyList<PackageDownloadRecord>> ProduceRecordsAsync(IVersionSet versionSet, AsOfData<PackageDownloads> data)
        {
            return ProduceRecordsAsync<PackageDownloadRecord>(versionSet, data.AsOfTimestamp, data.Pages);
        }

        public static async IAsyncEnumerable<IReadOnlyList<TRecord>> ProduceRecordsAsync<TRecord>(
            IVersionSet versionSet,
            DateTimeOffset asOfTimestamp,
            IAsyncEnumerable<IReadOnlyList<PackageDownloads>> data)
            where TRecord : IPackageDownloadRecord<TRecord>, new()
        {
            var idToVersions = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<long>>();
            const int pageSize = AsOfData<PackageDownloadRecord>.DefaultPageSize;
            var outputPage = new List<TRecord>(capacity: pageSize);

            await foreach (IReadOnlyList<PackageDownloads> page in data)
            {
                foreach (PackageDownloads entry in page)
                {
                    if (!NuGetVersion.TryParse(entry.Version, out var parsedVersion))
                    {
                        continue;
                    }

                    var normalizedVersion = parsedVersion.ToNormalizedString();
                    if (!versionSet.TryGetVersion(entry.Id, normalizedVersion, out normalizedVersion))
                    {
                        continue;
                    }

                    if (!versionSet.TryGetId(entry.Id, out var id))
                    {
                        continue;
                    }

                    if (!idToVersions.TryGetValue(id, out var versionToDownloads))
                    {
                        // Only write when we move to the next ID. This ensures all of the versions of a given ID are in the same segment.
                        if (idToVersions.Any())
                        {
                            foreach (var inner in WriteAndClear<TRecord>(asOfTimestamp, idToVersions, versionSet))
                            {
                                outputPage.Add(inner);
                                if (outputPage.Count >= pageSize)
                                {
                                    yield return outputPage;
                                    outputPage.Clear();
                                }
                            }
                        }

                        versionToDownloads = new CaseInsensitiveDictionary<long>();
                        idToVersions.Add(id, versionToDownloads);
                    }

                    versionToDownloads[normalizedVersion] = entry.Downloads;
                }
            }

            if (idToVersions.Any())
            {
                foreach (TRecord inner in WriteAndClear<TRecord>(asOfTimestamp, idToVersions, versionSet))
                {
                    outputPage.Add(inner);
                    if (outputPage.Count >= pageSize)
                    {
                        yield return outputPage;
                        outputPage.Clear();
                    }
                }
            }

            // Add IDs that are not mentioned in the data and therefore have no downloads.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                var lowerId = id.ToLowerInvariant();

                foreach (var version in versionSet.GetUncheckedVersions(id))
                {
                    outputPage.Add(new TRecord
                    {
                        AsOfTimestamp = asOfTimestamp,
                        Id = id,
                        LowerId = lowerId,
                        Identity = $"{lowerId}/{version.ToLowerInvariant()}",
                        TotalDownloads = 0,
                        Version = version,
                        Downloads = 0,
                    });
                    if (outputPage.Count >= pageSize)
                    {
                        yield return outputPage;
                        outputPage.Clear();
                    }
                }
            }

            if (outputPage.Count > 0)
            {
                yield return outputPage;
            }
        }

        private static IEnumerable<T> WriteAndClear<T>(
            DateTimeOffset asOfTimestamp,
            CaseInsensitiveDictionary<CaseInsensitiveDictionary<long>> idToVersions,
            IVersionSet versionSet) where T : IPackageDownloadRecord<T>, new()
        {
            foreach (var idPair in idToVersions)
            {
                var lowerId = idPair.Key.ToLowerInvariant();
                var totalDownloads = idPair.Value.Sum(x => x.Value);

                foreach (var versionPair in idPair.Value)
                {
                    yield return new T
                    {
                        AsOfTimestamp = asOfTimestamp,
                        Id = idPair.Key,
                        LowerId = lowerId,
                        Identity = $"{lowerId}/{versionPair.Key.ToLowerInvariant()}",
                        TotalDownloads = totalDownloads,
                        Version = versionPair.Key,
                        Downloads = versionPair.Value,
                    };
                }

                // Add versions that are not mentioned in the data and therefore have no downloads.
                foreach (var version in versionSet.GetUncheckedVersions(idPair.Key))
                {
                    yield return new T
                    {
                        AsOfTimestamp = asOfTimestamp,
                        Id = idPair.Key,
                        LowerId = lowerId,
                        Identity = $"{lowerId}/{version.ToLowerInvariant()}",
                        TotalDownloads = totalDownloads,
                        Version = version,
                        Downloads = 0,
                    };
                }
            }

            idToVersions.Clear();
        }
    }
}
