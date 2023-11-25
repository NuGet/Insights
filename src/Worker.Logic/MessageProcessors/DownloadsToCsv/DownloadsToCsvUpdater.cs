// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public class DownloadsToCsvUpdater : IAuxiliaryFileUpdater<AsOfData<PackageDownloads>>
    {
        private readonly PackageDownloadsClient _packageDownloadsClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public DownloadsToCsvUpdater(
            PackageDownloadsClient packageDownloadsClient,
            IOptions<NuGetInsightsWorkerSettings> options)
        {
            _packageDownloadsClient = packageDownloadsClient;
            _options = options;
        }

        public string OperationName => "DownloadsToCsv";
        public string BlobName => "downloads";
        public string ContainerName => _options.Value.PackageDownloadContainerName;
        public TimeSpan Frequency => _options.Value.DownloadToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.DownloadsV1Urls is not null && _options.Value.DownloadsV1Urls.Count > 0;
        public bool AutoStart => _options.Value.AutoStartDownloadToCsv;
        public Type RecordType => typeof(PackageDownloadRecord);

        public async Task<AsOfData<PackageDownloads>> GetDataAsync()
        {
            return await _packageDownloadsClient.GetAsync();
        }

        public async Task WriteAsync(IVersionSet versionSet, AsOfData<PackageDownloads> data, TextWriter writer)
        {
            var record = new PackageDownloadRecord { AsOfTimestamp = data.AsOfTimestamp };
            await WriteAsync(versionSet, record, data.Entries, writer);
        }

        public static async Task WriteAsync(IVersionSet versionSet, IPackageDownloadRecord record, IAsyncEnumerable<PackageDownloads> entries, TextWriter writer)
        {
            record.WriteHeader(writer);

            var idToVersions = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<long>>();
            await foreach (var entry in entries)
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
                        WriteAndClear(writer, record, idToVersions, versionSet);
                    }

                    versionToDownloads = new CaseInsensitiveDictionary<long>();
                    idToVersions.Add(id, versionToDownloads);
                }

                versionToDownloads[normalizedVersion] = entry.Downloads;
            }

            if (idToVersions.Any())
            {
                WriteAndClear(writer, record, idToVersions, versionSet);
            }

            // Add IDs that are not mentioned in the data and therefore have no downloads.
            foreach (var id in versionSet.GetUncheckedIds())
            {
                record.Id = id;
                record.LowerId = id.ToLowerInvariant();
                record.TotalDownloads = 0;

                foreach (var version in versionSet.GetUncheckedVersions(id))
                {
                    record.Version = version;
                    record.Identity = $"{record.LowerId}/{record.Version.ToLowerInvariant()}";
                    record.Downloads = 0;

                    record.Write(writer);
                }
            }
        }

        private static void WriteAndClear(TextWriter writer, IPackageDownloadRecord record, CaseInsensitiveDictionary<CaseInsensitiveDictionary<long>> idToVersions, IVersionSet versionSet)
        {
            foreach (var idPair in idToVersions)
            {
                record.Id = idPair.Key;
                record.LowerId = idPair.Key.ToLowerInvariant();
                record.TotalDownloads = idPair.Value.Sum(x => x.Value);

                foreach (var versionPair in idPair.Value)
                {
                    record.Version = versionPair.Key;
                    record.Identity = $"{record.LowerId}/{record.Version.ToLowerInvariant()}";
                    record.Downloads = versionPair.Value;

                    record.Write(writer);
                }

                // Add versions that are not mentioned in the data and therefore have no downloads.
                foreach (var version in versionSet.GetUncheckedVersions(idPair.Key))
                {
                    record.Version = version;
                    record.Identity = $"{record.LowerId}/{version.ToLowerInvariant()}";
                    record.Downloads = 0;

                    record.Write(writer);
                }
            }

            idToVersions.Clear();
        }
    }
}
