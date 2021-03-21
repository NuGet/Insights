using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public class StreamWriterUpdaterProcessor<T> : ITaskStateMessageProcessor<StreamWriterUpdaterMessage<T>>
        where T : IAsyncDisposable, IAsOfData
    {
        private const string AsOfTimestampMetadata = "asOfTimestamp";
        private const string RawSizeBytesMetadata = "rawSizeBytes";

        private readonly NewServiceClientFactory _serviceClientFactory;
        private readonly IStreamWriterUpdater<T> _updater;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<StreamWriterUpdaterProcessor<T>> _logger;

        public StreamWriterUpdaterProcessor(
            NewServiceClientFactory serviceClientFactory,
            IStreamWriterUpdater<T> updater,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<StreamWriterUpdaterProcessor<T>> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _updater = updater;
            _options = options;
            _logger = logger;
        }

        public async Task<bool> ProcessAsync(StreamWriterUpdaterMessage<T> message, long dequeueCount)
        {
            await InitializeAsync();

            await using var data = await _updater.GetDataAsync();

            var latestBlob = await GetBlobAsync($"latest_{_updater.BlobName}.csv.gz");

            BlobRequestConditions latestRequestConditions;
            try
            {
                BlobProperties properties = await latestBlob.GetPropertiesAsync();
                if (properties.Metadata.TryGetValue(AsOfTimestampMetadata, out var unparsedAsOfTimestamp)
                    && DateTimeOffset.TryParse(unparsedAsOfTimestamp, out var latestAsOfTimestamp)
                    && latestAsOfTimestamp == data.AsOfTimestamp)
                {
                    _logger.LogInformation("The {OperationName} data from {AsOfTimestamp:O} already exists.", _updater.OperationName, data.AsOfTimestamp);
                    return true;
                }

                latestRequestConditions = new BlobRequestConditions { IfMatch = properties.ETag };
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                latestRequestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
            }

            if (_options.Value.OnlyKeepLatestInStreamWriterUpdater)
            {
                await WriteDataAsync(data, latestBlob);
            }
            else
            {
                var dataBlob = await GetBlobAsync($"{_updater.BlobName}_{StorageUtility.GetDescendingId(data.AsOfTimestamp)}.csv.gz");
                (var uncompressedLength, var etag) = await WriteDataAsync(data, dataBlob);
                var dataRequestConditions = new BlobRequestConditions { IfMatch = etag };
                await CopyLatestAsync(uncompressedLength, data.AsOfTimestamp, dataBlob, dataRequestConditions, latestBlob, latestRequestConditions);
            }

            return true;
        }

        private async Task<(long uncompressedLength, ETag etag)> WriteDataAsync(T data, BlobClient destBlob)
        {
            (var stream, var uncompressedLength) = await SerializeDataAsync(data);

            using (stream)
            {
                BlobContentInfo info = await destBlob.UploadAsync(
                    stream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = "text/plain",
                            ContentEncoding = "gzip",
                        },
                        Metadata = new Dictionary<string, string>
                        {
                            {
                                RawSizeBytesMetadata,
                                uncompressedLength.ToString() // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
                            },
                            {
                                AsOfTimestampMetadata,
                                data.AsOfTimestamp.ToString("O")
                            },
                        },
                    });

                return (uncompressedLength, info.ETag);
            }
        }

        private async Task<(MemoryStream stream, long uncompressedLength)> SerializeDataAsync(T data)
        {
            var memoryStream = new MemoryStream();

            long uncompressedLength;
            using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                using var countingStream = new CountingWriterStream(gzipStream);
                using var writer = new StreamWriter(countingStream);

                await _updater.WriteAsync(data, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();

                uncompressedLength = countingStream.Length;
            }

            memoryStream.Position = 0;

            return (memoryStream, uncompressedLength);
        }

        private async Task CopyLatestAsync(
            long uncompressedLength,
            DateTimeOffset asOfTimestamp,
            BlobClient dataBlob,
            BlobRequestConditions dataRequestConditions,
            BlobClient latestBlob,
            BlobRequestConditions latestRequestConditions)
        {
            var operation = await latestBlob.StartCopyFromUriAsync(
                dataBlob.Uri,
                new BlobCopyFromUriOptions
                {
                    SourceConditions = dataRequestConditions,
                    DestinationConditions = latestRequestConditions,
                    Metadata = new Dictionary<string, string>
                    {
                        {
                            RawSizeBytesMetadata,
                            uncompressedLength.ToString() // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
                        },
                        {
                            AsOfTimestampMetadata,
                            asOfTimestamp.ToString("O")
                        },
                    },
                });

            await operation.WaitForCompletionAsync();
        }

        private async Task InitializeAsync()
        {
            await (await GetContainerAsync()).CreateIfNotExistsAsync(retry: true);
        }

        private async Task<BlobClient> GetBlobAsync(string blobName)
        {
            return (await GetContainerAsync()).GetBlobClient(blobName);
        }

        private async Task<BlobContainerClient> GetContainerAsync()
        {
            return (await _serviceClientFactory.GetBlobServiceClientAsync()).GetBlobContainerClient(_updater.ContainerName);
        }
    }
}
