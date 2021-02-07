using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvProcessor : ILoopingMessageProcessor<DownloadsToCsvMessage>
    {
        private const string AsOfTimestampMetadata = "asOfTimestamp";
        private const string RawSizeBytesMetadata = "rawSizeBytes";

        private readonly IPackageDownloadsClient _packageDownloadsClient;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly DownloadsToCsvService _service;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<DownloadsToCsvProcessor> _logger;

        public DownloadsToCsvProcessor(
            IPackageDownloadsClient packageDownloadsClient,
            ServiceClientFactory serviceClientFactory,
            DownloadsToCsvService service,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<DownloadsToCsvProcessor> logger)
        {
            _packageDownloadsClient = packageDownloadsClient;
            _serviceClientFactory = serviceClientFactory;
            _service = service;
            _options = options;
            _logger = logger;
        }

        public string LeaseName => "DownloadsToCsv";

        public async Task StartAsync()
        {
            await _service.StartAsync(loop: true, notBefore: TimeSpan.FromMinutes(30));
        }

        public async Task<bool> ProcessAsync(DownloadsToCsvMessage message, int dequeueCount)
        {
            await InitializeAsync();

            await using var set = await _packageDownloadsClient.GetPackageDownloadSetAsync(etag: null);

            var latestBlob = GetBlob($"latest_downloads.csv.gz");
            if (await latestBlob.ExistsAsync()
                && latestBlob.Metadata.TryGetValue(AsOfTimestampMetadata, out var unparsedAsOfTimestamp)
                && DateTimeOffset.TryParse(unparsedAsOfTimestamp, out var latestAsOfTimestamp)
                && latestAsOfTimestamp == set.AsOfTimestamp)
            {
                _logger.LogInformation("The downloads from {AsOfTimestamp:O} already exists.", set.AsOfTimestamp);
                return true;
            }

            var downloadsBlob = GetBlob($"downloads_{StorageUtility.GetDescendingId(set.AsOfTimestamp)}.csv.gz");

            await WriteDownloadsAsync(set, downloadsBlob);
            await CopyLatestAsync(set.AsOfTimestamp, downloadsBlob, latestBlob);

            return true;
        }

        private static async Task CopyLatestAsync(DateTimeOffset asOfTimestamp, CloudBlockBlob downloadsBlob, CloudBlockBlob latestBlob)
        {
            var sourceAccessCondition = AccessCondition.GenerateIfMatchCondition(downloadsBlob.Properties.ETag);

            AccessCondition destAccessCondition;
            if (latestBlob.Properties.ETag == null)
            {
                destAccessCondition = AccessCondition.GenerateIfNotExistsCondition();
            }
            else
            {
                destAccessCondition = AccessCondition.GenerateIfMatchCondition(latestBlob.Properties.ETag);
            }

            latestBlob.Metadata[RawSizeBytesMetadata] = downloadsBlob.Metadata[RawSizeBytesMetadata];
            latestBlob.Metadata[AsOfTimestampMetadata] = asOfTimestamp.ToString("O");

            await latestBlob.StartCopyAsync(
                downloadsBlob,
                sourceAccessCondition,
                destAccessCondition,
                options: null,
                operationContext: null);

            var first = true;
            do
            {
                if (!first)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    first = false;
                }

                await latestBlob.FetchAttributesAsync();
            }
            while (latestBlob.CopyState.Status == CopyStatus.Pending);

            if (latestBlob.CopyState.Status != CopyStatus.Success)
            {
                throw new InvalidOperationException($"Copying the downloads blob ended with an unexpected status: {latestBlob.CopyState.Status}");
            }
        }

        private async Task WriteDownloadsAsync(PackageDownloadSet set, CloudBlockBlob destBlob)
        {
            destBlob.Properties.ContentType = "text/plain";
            destBlob.Properties.ContentEncoding = "gzip";

            long uncompressedLength;
            using (var destStream = await destBlob.OpenWriteAsync())
            {
                using var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal);
                using var countingWriterStream = new CountingWriterStream(gzipStream);
                using var writer = new StreamWriter(countingWriterStream);

                await WriteAsync(set.Downloads, set.AsOfTimestamp, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();
                await destStream.FlushAsync();

                uncompressedLength = countingWriterStream.Length;
            }

            destBlob.Metadata[RawSizeBytesMetadata] = uncompressedLength.ToString(); // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
            await destBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(destBlob.Properties.ETag), options: null, operationContext: null);
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageDownloadsContainerName)
                .GetBlockBlobReference(blobName);
        }

        private async Task InitializeAsync()
        {
            await _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageDownloadsContainerName)
                .CreateIfNotExistsAsync(retry: true);
        }

        private async Task WriteAsync(IAsyncEnumerable<PackageDownloads> entries, DateTimeOffset asOfTimestamp, StreamWriter writer)
        {
            var record = new PackageDownloadRecord { AsOfTimestamp = asOfTimestamp };

            var idToVersions = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in entries)
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
