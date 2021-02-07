using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvUpdater : IStreamWriterUpdater<PackageDownloadSet>
    {
        private readonly IPackageDownloadsClient _packageDownloadsClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public DownloadsToCsvUpdater(
            IPackageDownloadsClient packageDownloadsClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _packageDownloadsClient = packageDownloadsClient;
            _options = options;
        }

        public string OperationName => "DownloadsToCsv";
        public string BlobName => "downloads";
        public string ContainerName => _options.Value.PackageDownloadsContainerName;
        public TimeSpan LoopFrequency => TimeSpan.FromMinutes(30);

        public async Task<PackageDownloadSet> GetDataAsync()
        {
            return await _packageDownloadsClient.GetPackageDownloadSetAsync(etag: null);
        }

        public async Task WriteAsync(PackageDownloadSet data, StreamWriter writer)
        {
            var record = new PackageDownloadRecord { AsOfTimestamp = data.AsOfTimestamp };

            var idToVersions = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in data.Downloads)
            {
                if (!idToVersions.TryGetValue(entry.Id, out var versionToDownloads))
                {
                    // Only write when we move to the next ID. This ensures all of the versions of a given ID are in the same segment.
                    if (idToVersions.Any())
                    {
                        await WriteAndClearAsync(writer, record, idToVersions);
                    }

                    versionToDownloads = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                    idToVersions.Add(entry.Id, versionToDownloads);
                }

                var normalizedVersion = NuGetVersion.Parse(entry.Version).ToNormalizedString();
                versionToDownloads[normalizedVersion] = entry.Downloads;
            }

            if (idToVersions.Any())
            {
                await WriteAndClearAsync(writer, record, idToVersions);
            }
        }

        private static async Task WriteAndClearAsync(StreamWriter writer, PackageDownloadRecord record, Dictionary<string, Dictionary<string, long>> idToVersions)
        {
            foreach (var idPair in idToVersions)
            {
                record.Id = idPair.Key;
                record.TotalDownloads = idPair.Value.Sum(x => x.Value);

                foreach (var versionPair in idPair.Value)
                {
                    record.Version = versionPair.Key;
                    record.Downloads = versionPair.Value;

                    await record.WriteAsync(writer);
                }
            }

            idToVersions.Clear();
        }
    }
}
