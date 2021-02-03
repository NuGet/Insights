using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Worker.OwnersToCsv
{
    public class OwnersToCsvProcessor : ILoopingMessageProcessor<OwnersToCsvMessage>
    {
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

            var destBlob = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageOwnersContainerName)
                .GetBlockBlobReference($"owners_{StorageUtility.GetDescendingId(set.AsOfTimestamp)}.csv.gz");

            if (await destBlob.ExistsAsync())
            {
                _logger.LogInformation("The owners from {AsOfTimestamp:O} already exists.", set.AsOfTimestamp);
                return true;
            }

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

            return true;
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
