using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvProcessor : ITaskStateMessageProcessor<DownloadsToCsvMessage>
    {
        private readonly IPackageDownloadsClient _packageDownloadsClient;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly DownloadsToCsvService _service;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<DownloadsToCsvProcessor> _logger;

        public DownloadsToCsvProcessor(
            IPackageDownloadsClient packageDownloadsClient,
            ServiceClientFactory serviceClientFactory,
            DownloadsToCsvService service,
            AutoRenewingStorageLeaseService leaseService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<DownloadsToCsvProcessor> logger)
        {
            _packageDownloadsClient = packageDownloadsClient;
            _serviceClientFactory = serviceClientFactory;
            _service = service;
            _leaseService = leaseService;
            _options = options;
            _logger = logger;
        }

        public async Task<bool> ProcessAsync(DownloadsToCsvMessage message, int dequeueCount)
        {
            // Only one function -- looping or non-looping should be executing at a time.
            await using (var lease = await _leaseService.TryAcquireAsync("DownloadsToCsv"))
            {
                if (message.Loop)
                {
                    return await ProcessLoopingAsync(lease);
                }
                else
                {
                    return await ProcessNonLoopingAsync(lease);
                }
            }
        }

        private async Task<bool> ProcessNonLoopingAsync(AutoRenewingStorageLeaseResult lease)
        {
            if (!lease.Acquired)
            {
                // If the message is non-looping and the lease is acquired, ignore this message.
                return true;
            }

            return await ProcessAsync();
        }

        private async Task<bool> ProcessLoopingAsync(AutoRenewingStorageLeaseResult lease)
        {
            if (!lease.Acquired)
            {
                // If the message is looping and the lease is not acquired, ignore this message but schedule the next one.
                await ScheduleLoopAsync();
                return true;
            }

            await using (var loopLease = await _leaseService.TryAcquireAsync("DownloadsToCsv-Loop"))
            {
                if (!lease.Acquired)
                {
                    // If there is another loop message already running, ignore this message.
                    return true;
                }

                if (await ProcessAsync())
                {
                    // If the work is completed successfully, schedule the next one.
                    await ScheduleLoopAsync();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private async Task ScheduleLoopAsync()
        {
            await _service.StartAsync(loop: true, notBefore: TimeSpan.FromMinutes(30));
        }

        private async Task<bool> ProcessAsync()
        {
            await using var set = await _packageDownloadsClient.GetPackageDownloadSetAsync(etag: null);

            var destBlob = _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageDownloadsContainerName)
                .GetBlockBlobReference($"downloads_{StorageUtility.GetDescendingId(set.AsOfTimestamp)}.csv.gz");

            if (await destBlob.ExistsAsync())
            {
                _logger.LogInformation("The downloads from {AsOfTimestamp:O} already exists.", set.AsOfTimestamp);
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

                await WriteAsync(set.Downloads, set.AsOfTimestamp, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();
                await destStream.FlushAsync();

                uncompressedLength = countingWriterStream.Length;
            }

            destBlob.Metadata["rawSizeBytes"] = uncompressedLength.ToString(); // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
            await destBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(destBlob.Properties.ETag), options: null, operationContext: null);

            return true;
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
