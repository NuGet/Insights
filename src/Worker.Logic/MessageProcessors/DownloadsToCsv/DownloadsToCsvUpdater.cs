// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;
using NuGet.Versioning;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public class DownloadsToCsvUpdater : IAuxiliaryFileUpdater<PackageDownloadSet>
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
        public string BlobName => "downloads";
        public string ContainerName => _options.Value.PackageDownloadsContainerName;
        public TimeSpan Frequency => _options.Value.DownloadToCsvFrequency;
        public bool HasRequiredConfiguration => _options.Value.DownloadsV1Url != null;
        public bool AutoStart => _options.Value.AutoStartDownloadToCsv;
        public Type RecordType => typeof(PackageDownloadRecord);

        public async Task<PackageDownloadSet> GetDataAsync()
        {
            return await _packageDownloadsClient.GetPackageDownloadSetAsync(etag: null);
        }

        public async Task WriteAsync(IVersionSet versionSet, PackageDownloadSet data, StreamWriter writer)
        {
            var record = new PackageDownloadRecord { AsOfTimestamp = data.AsOfTimestamp };
            record.WriteHeader(writer);

            var idToVersions = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in data.Downloads)
            {
                var normalizedVersion = NuGetVersion.Parse(entry.Version).ToNormalizedString();
                if (!versionSet.DidVersionEverExist(entry.Id, normalizedVersion))
                {
                    continue;
                }

                // Mark the ID as checked, since we have an existing version and will write at least one record for it.
                versionSet.DidIdEverExist(entry.Id);

                if (!idToVersions.TryGetValue(entry.Id, out var versionToDownloads))
                {
                    // Only write when we move to the next ID. This ensures all of the versions of a given ID are in the same segment.
                    if (idToVersions.Any())
                    {
                        WriteAndClear(writer, record, idToVersions, versionSet);
                    }

                    versionToDownloads = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    idToVersions.Add(entry.Id, versionToDownloads);
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

        private static void WriteAndClear(StreamWriter writer, PackageDownloadRecord record, Dictionary<string, Dictionary<string, long>> idToVersions, IVersionSet versionSet)
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
