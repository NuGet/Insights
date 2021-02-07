using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Worker.StreamWriterUpdater
{
    public class StreamWriterUpdaterProcessor<T> : ILoopingMessageProcessor<StreamWriterUpdaterMessage<T>>
        where T : IAsyncDisposable, IAsOfData
    {
        private const string AsOfTimestampMetadata = "asOfTimestamp";
        private const string RawSizeBytesMetadata = "rawSizeBytes";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IStreamWriterUpdater<T> _updater;
        private readonly IStreamWriterUpdaterService<T> _service;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;
        private readonly ILogger<StreamWriterUpdaterProcessor<T>> _logger;

        public StreamWriterUpdaterProcessor(
            ServiceClientFactory serviceClientFactory,
            IStreamWriterUpdater<T> processor,
            IStreamWriterUpdaterService<T> service,
            IOptions<ExplorePackagesWorkerSettings> options,
            ILogger<StreamWriterUpdaterProcessor<T>> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _updater = processor;
            _service = service;
            _options = options;
            _logger = logger;
        }

        public string LeaseName => _updater.OperationName;

        public async Task StartAsync()
        {
            await _service.StartAsync(loop: true, _updater.LoopFrequency);
        }

        public async Task<bool> ProcessAsync(StreamWriterUpdaterMessage<T> message, int dequeueCount)
        {
            await InitializeAsync();

            await using var data = await _updater.GetDataAsync();

            var latestBlob = GetBlob($"latest_{_updater.BlobName}.csv.gz");

            if (_options.Value.OnlyKeepLatestInStreamWriterUpdater)
            {
                await WriteDataAsync(data, latestBlob);
            }
            else
            {
                if (await latestBlob.ExistsAsync()
                    && latestBlob.Metadata.TryGetValue(AsOfTimestampMetadata, out var unparsedAsOfTimestamp)
                    && DateTimeOffset.TryParse(unparsedAsOfTimestamp, out var latestAsOfTimestamp)
                    && latestAsOfTimestamp == data.AsOfTimestamp)
                {
                    _logger.LogInformation("The {OperationName} data from {AsOfTimestamp:O} already exists.", _updater.OperationName, data.AsOfTimestamp);
                    return true;
                }

                var dataBlob = GetBlob($"{_updater.BlobName}_{StorageUtility.GetDescendingId(data.AsOfTimestamp)}.csv.gz");

                await WriteDataAsync(data, dataBlob);
                await CopyLatestAsync(data.AsOfTimestamp, dataBlob, latestBlob);
            }

            return true;
        }

        private async Task WriteDataAsync(T data, CloudBlockBlob destBlob)
        {
            destBlob.Properties.ContentType = "text/plain";
            destBlob.Properties.ContentEncoding = "gzip";

            long uncompressedLength;
            using (var destStream = await destBlob.OpenWriteAsync())
            {
                using var gzipStream = new GZipStream(destStream, CompressionLevel.Optimal);
                using var countingWriterStream = new CountingWriterStream(gzipStream);
                using var writer = new StreamWriter(countingWriterStream);

                await _updater.WriteAsync(data, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();
                await destStream.FlushAsync();

                uncompressedLength = countingWriterStream.Length;
            }

            destBlob.Metadata["rawSizeBytes"] = uncompressedLength.ToString(); // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
            await destBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(destBlob.Properties.ETag), options: null, operationContext: null);
        }

        private async Task CopyLatestAsync(DateTimeOffset asOfTimestamp, CloudBlockBlob dataBlob, CloudBlockBlob latestBlob)
        {
            var sourceAccessCondition = AccessCondition.GenerateIfMatchCondition(dataBlob.Properties.ETag);

            AccessCondition destAccessCondition;
            if (latestBlob.Properties.ETag == null)
            {
                destAccessCondition = AccessCondition.GenerateIfNotExistsCondition();
            }
            else
            {
                destAccessCondition = AccessCondition.GenerateIfMatchCondition(latestBlob.Properties.ETag);
            }

            latestBlob.Metadata[RawSizeBytesMetadata] = dataBlob.Metadata[RawSizeBytesMetadata];
            latestBlob.Metadata[AsOfTimestampMetadata] = asOfTimestamp.ToString("O");

            await latestBlob.StartCopyAsync(
                dataBlob,
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
                throw new InvalidOperationException($"Copying the {_updater.OperationName} data to the latest ended with an unexpected status: {latestBlob.CopyState.Status}");
            }
        }

        private async Task InitializeAsync()
        {
            await GetContainer().CreateIfNotExistsAsync(retry: true);
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return GetContainer().GetBlockBlobReference(blobName);
        }

        private CloudBlobContainer GetContainer()
        {
            return _serviceClientFactory
                .GetStorageAccount()
                .CreateCloudBlobClient()
                .GetContainerReference(_updater.ContainerName);
        }
    }
}
