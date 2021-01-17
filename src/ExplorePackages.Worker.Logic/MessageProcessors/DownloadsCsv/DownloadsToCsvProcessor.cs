using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public class DownloadsToCsvProcessor : ITaskStateMessageProcessor<DownloadsToCsvMessage>
    {
        private readonly HttpSource _httpSource;
        private readonly TempStreamService _tempStreamService;
        private readonly TaskStateStorageService _taskStateStorageService;
        private readonly DownloadsV1JsonDeserializer _deserializer;
        private readonly IPackageDownloadsClient _packageDownloadsClient;
        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly MessageEnqueuer _messageEnqueuer;
        private readonly DownloadsToCsvService _service;
        private readonly AutoRenewingStorageLeaseService _leaseService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<DownloadsToCsvProcessor> _logger;

        public DownloadsToCsvProcessor(
            HttpSource httpSource,
            TempStreamService tempStreamService,
            TaskStateStorageService taskStateStorageService,
            DownloadsV1JsonDeserializer deserializer,
            IPackageDownloadsClient packageDownloadsClient,
            ServiceClientFactory serviceClientFactory,
            MessageEnqueuer messageEnqueuer,
            DownloadsToCsvService service,
            AutoRenewingStorageLeaseService leaseService,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<DownloadsToCsvProcessor> logger)
        {
            _httpSource = httpSource;
            _tempStreamService = tempStreamService;
            _taskStateStorageService = taskStateStorageService;
            _deserializer = deserializer;
            _packageDownloadsClient = packageDownloadsClient;
            _serviceClientFactory = serviceClientFactory;
            _messageEnqueuer = messageEnqueuer;
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
            var initialAsOfTimestamp = await GetAsOfTimestampAsync();
            if (await GetDestinationBlob(initialAsOfTimestamp).ExistsAsync())
            {
                _logger.LogInformation("The downloads from {AsOfTimestamp:O} already exists.", initialAsOfTimestamp);
                return true;
            }

            (var result, var asOfTimestamp) = await DownloadAsync();
            using (result)
            {
                if (result.Type == TempStreamResultType.SemaphoreNotAvailable)
                {
                    return false;
                }

                using var reader = new StreamReader(result.Stream);

                var destinationBlob = GetDestinationBlob(asOfTimestamp);
                destinationBlob.Properties.ContentType = "text/plain";
                destinationBlob.Properties.ContentEncoding = "gzip";

                using var destStream = await destinationBlob.OpenWriteAsync();
                using var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal);
                using var writer = new StreamWriter(gzipStream);

                await WriteAsync(reader, asOfTimestamp, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();
                await destStream.FlushAsync();

                return true;
            }
        }

        private async Task<DateTimeOffset> GetAsOfTimestampAsync()
        {
            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessResponseAsync(
                new HttpSourceRequest(() => HttpRequestMessageFactory.Create(HttpMethod.Head, _options.Value.DownloadsV1Url, nuGetLogger)),
                response => Task.FromResult(response.Content.Headers.LastModified.Value.ToUniversalTime()),
                nuGetLogger,
                CancellationToken.None);
        }

        private CloudBlockBlob GetDestinationBlob(DateTimeOffset asOfTimestamp)
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_options.Value.PackageDownloadsContainerName)
                .GetBlockBlobReference($"downloads_{StorageUtility.GetDescendingId(asOfTimestamp)}.csv.gz");
        }

        private async Task<(TempStreamResult Result, DateTimeOffset AsOfTimestamp)> DownloadAsync()
        {
            var nuGetLogger = _logger.ToNuGetLogger();
            var writer = _tempStreamService.GetWriter();

            TempStreamResult result = null;
            DateTimeOffset asOfTimestamp = default;
            try
            {
                do
                {
                    result = await _httpSource.ProcessResponseAsync(
                        new HttpSourceRequest(_options.Value.DownloadsV1Url, nuGetLogger),
                        async response =>
                        {
                            response.EnsureSuccessStatusCode();
                            using var networkStream = await response.Content.ReadAsStreamAsync();
                            result = await writer.CopyToTempStreamAsync(networkStream, response.Content.Headers.ContentLength.Value);
                            asOfTimestamp = response.Content.Headers.LastModified.Value.ToUniversalTime();
                            return result;
                        },
                        nuGetLogger,
                        CancellationToken.None);
                }
                while (result.Type == TempStreamResultType.NeedNewStream);

                return (result, asOfTimestamp);
            }
            catch
            {
                result?.Dispose();
                throw;
            }
        }

        private async Task WriteAsync(StreamReader reader, DateTimeOffset asOfTimestamp, StreamWriter writer)
        {
            var record = new PackageDownloadRecord { AsOfTimestamp = asOfTimestamp };

            var idToVersions = new Dictionary<string, Dictionary<string, long>>(StringComparer.OrdinalIgnoreCase);
            await foreach (var entry in _deserializer.DeserializeAsync(reader))
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
