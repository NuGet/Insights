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
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public class OwnersToCsvProcessor : ILoopingMessageProcessor<OwnersToCsvMessage>
    {
        private const string AsOfTimestampMetadata = "asOfTimestamp";
        private const string RawSizeBytesMetadata = "rawSizeBytes";

        private readonly PackageOwnersClient _packageOwnersClient;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly OwnersToCsvService _service;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<OwnersToCsvProcessor> _logger;

        public OwnersToCsvProcessor(
            PackageOwnersClient packageOwnersClient,
            ServiceClientFactory serviceClientFactory,
            OwnersToCsvService service,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<OwnersToCsvProcessor> logger)
        {
            _packageOwnersClient = packageOwnersClient;
            _serviceClientFactory = serviceClientFactory;
            _service = service;
            _options = options;
            _logger = logger;
        }

        public string LeaseName => "OwnersToCsv";

        public async Task StartAsync()
        {
            await _service.StartAsync(loop: true, notBefore: TimeSpan.FromMinutes(10));
        }

        public async Task<bool> ProcessAsync(OwnersToCsvMessage message, int dequeueCount)
        {
            await InitializeAsync();

            await using var set = await _packageOwnersClient.GetPackageOwnerSetAsync();

            var latestBlob = GetBlob($"latest_owners.csv.gz");
            if (await latestBlob.ExistsAsync()
                && latestBlob.Metadata.TryGetValue(AsOfTimestampMetadata, out var unparsedAsOfTimestamp)
                && DateTimeOffset.TryParse(unparsedAsOfTimestamp, out var latestAsOfTimestamp)
                && latestAsOfTimestamp == set.AsOfTimestamp)
            {
                _logger.LogInformation("The owners from {AsOfTimestamp:O} already exists.", set.AsOfTimestamp);
                return true;
            }

            var ownersBlob = GetBlob($"owners_{StorageUtility.GetDescendingId(set.AsOfTimestamp)}.csv.gz");

            await WriteOwnersAsync(set, ownersBlob);
            await CopyLatestAsync(set.AsOfTimestamp, ownersBlob, latestBlob);

            return true;
        }

        private static async Task CopyLatestAsync(DateTimeOffset asOfTimestamp, CloudBlockBlob ownersBlob, CloudBlockBlob latestBlob)
        {
            var sourceAccessCondition = AccessCondition.GenerateIfMatchCondition(ownersBlob.Properties.ETag);

            AccessCondition destAccessCondition;
            if (latestBlob.Properties.ETag == null)
            {
                destAccessCondition = AccessCondition.GenerateIfNotExistsCondition();
            }
            else
            {
                destAccessCondition = AccessCondition.GenerateIfMatchCondition(latestBlob.Properties.ETag);
            }

            latestBlob.Metadata[RawSizeBytesMetadata] = ownersBlob.Metadata[RawSizeBytesMetadata];
            latestBlob.Metadata[AsOfTimestampMetadata] = asOfTimestamp.ToString("O");

            await latestBlob.StartCopyAsync(
                ownersBlob,
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
                throw new InvalidOperationException($"Copying the owners blob ended with an unexpected status: {latestBlob.CopyState.Status}");
            }
        }

        private async Task WriteOwnersAsync(PackageOwnerSet set, CloudBlockBlob destBlob)
        {
            destBlob.Properties.ContentType = "text/plain";
            destBlob.Properties.ContentEncoding = "gzip";

            long uncompressedLength;
            using (var destStream = await destBlob.OpenWriteAsync())
            {
                using var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal);
                using var countingWriterStream = new CountingWriterStream(gzipStream);
                using var writer = new StreamWriter(countingWriterStream);

                await WriteAsync(set.Owners, set.AsOfTimestamp, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();
                await destStream.FlushAsync();

                uncompressedLength = countingWriterStream.Length;
            }

            destBlob.Metadata["rawSizeBytes"] = uncompressedLength.ToString(); // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
            await destBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(destBlob.Properties.ETag), options: null, operationContext: null);
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageOwnersContainerName)
                .GetBlockBlobReference(blobName);
        }

        private async Task InitializeAsync()
        {
            await _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageOwnersContainerName)
                .CreateIfNotExistsAsync(retry: true);
        }

        private async Task WriteAsync(IAsyncEnumerable<PackageOwner> entries, DateTimeOffset asOfTimestamp, StreamWriter writer)
        {
            var record = new PackageOwnerRecord { AsOfTimestamp = asOfTimestamp };

            var idToOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in entries)
            {
                if (!idToOwners.TryGetValue(entry.Id, out var owners))
                {
                    // Only write when we move to the next ID. This ensures all of the owners of a given ID are in the same record.
                    if (idToOwners.Any())
                    {
                        await WriteAndClearAsync(writer, record, idToOwners);
                    }

                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    idToOwners.Add(entry.Id, owners);
                }

                owners.Add(entry.Username);
            }

            if (idToOwners.Any())
            {
                await WriteAndClearAsync(writer, record, idToOwners);
            }
        }

        private static async Task WriteAndClearAsync(StreamWriter writer, PackageOwnerRecord record, Dictionary<string, HashSet<string>> idToOwners)
        {
            foreach (var pair in idToOwners)
            {
                record.Id = pair.Key;
                record.Owners = JsonConvert.SerializeObject(pair.Value.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                await record.WriteAsync(writer);
            }

            idToOwners.Clear();
        }
    }
}
