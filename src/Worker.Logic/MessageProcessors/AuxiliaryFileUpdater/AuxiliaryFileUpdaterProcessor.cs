// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO.Compression;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.AuxiliaryFileUpdater
{
    public class AuxiliaryFileUpdaterProcessor<T> : ITaskStateMessageProcessor<AuxiliaryFileUpdaterMessage<T>> where T : IAsOfData
    {
        private const string AsOfTimestampMetadata = "asOfTimestamp";
        private const string VersionSetCommitTimestampMetadata = "versionSetCommitTimestamp";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly IVersionSetProvider _versionSetProvider;
        private readonly IAuxiliaryFileUpdater<T> _updater;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;
        private readonly ILogger<AuxiliaryFileUpdaterProcessor<T>> _logger;

        public AuxiliaryFileUpdaterProcessor(
            ServiceClientFactory serviceClientFactory,
            IVersionSetProvider versionSetProvider,
            IAuxiliaryFileUpdater<T> updater,
            IOptions<NuGetInsightsWorkerSettings> options,
            ILogger<AuxiliaryFileUpdaterProcessor<T>> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _versionSetProvider = versionSetProvider;
            _updater = updater;
            _options = options;
            _logger = logger;
        }

        public static string GetLatestBlobName(string blobName)
        {
            return $"latest_{blobName}.csv.gz";
        }

        public async Task<TaskStateProcessResult> ProcessAsync(AuxiliaryFileUpdaterMessage<T> message, TaskState taskState, long dequeueCount)
        {
            await using var data = await _updater.GetDataAsync();

            var latestBlob = await GetBlobAsync(GetLatestBlobName(_updater.BlobName));

            BlobRequestConditions latestRequestConditions;
            BlobProperties properties = null;
            try
            {
                properties = await latestBlob.GetPropertiesAsync();
                latestRequestConditions = new BlobRequestConditions { IfMatch = properties.ETag };
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                latestRequestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
            }

            using var versionSetHandle = await _versionSetProvider.GetAsync();

            if (properties != null
                && properties.Metadata.TryGetValue(AsOfTimestampMetadata, out var unparsedAsOfTimestamp)
                && DateTimeOffset.TryParse(unparsedAsOfTimestamp, out var latestAsOfTimestamp)
                && latestAsOfTimestamp == data.AsOfTimestamp)
            {
                if (properties.Metadata.TryGetValue(VersionSetCommitTimestampMetadata, out var unparsedVersionSetCommitTimestamp)
                    && DateTimeOffset.TryParse(unparsedVersionSetCommitTimestamp, out var versionSetCommitTimestamp)
                    && versionSetCommitTimestamp == versionSetHandle.Value.CommitTimestamp)
                {
                    _logger.LogInformation(
                        "The {OperationName} data from {AsOfTimestamp:O} with version set commit timestamp {VersionSetCommitTimestamp:O} already exists.",
                        _updater.OperationName,
                        data.AsOfTimestamp,
                        versionSetCommitTimestamp);
                    return TaskStateProcessResult.Complete;
                }
            }

            if (_options.Value.OnlyKeepLatestInAuxiliaryFileUpdater)
            {
                await WriteDataAsync(versionSetHandle.Value, data, latestBlob);
            }
            else
            {
                var dataBlob = await GetBlobAsync($"{_updater.BlobName}_{StorageUtility.GetDescendingId(data.AsOfTimestamp)}.csv.gz");
                (var uncompressedLength, var recordCount, var etag) = await WriteDataAsync(versionSetHandle.Value, data, dataBlob);
                var dataRequestConditions = new BlobRequestConditions { IfMatch = etag };
                await CopyLatestAsync(
                    uncompressedLength,
                    recordCount,
                    data.AsOfTimestamp,
                    versionSetHandle.Value.CommitTimestamp,
                    dataBlob,
                    dataRequestConditions,
                    latestBlob,
                    latestRequestConditions);
            }

            return TaskStateProcessResult.Complete;
        }

        private async Task<(long uncompressedLength, long recordCount, ETag etag)> WriteDataAsync(IVersionSet versionSet, T data, BlobClient destBlob)
        {
            (var stream, var uncompressedLength, var recordCount) = await SerializeDataAsync(versionSet, data);

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
                        Metadata = GetMetadata(uncompressedLength, recordCount, data.AsOfTimestamp, versionSet.CommitTimestamp)
                    });

                return (uncompressedLength, recordCount, info.ETag);
            }
        }

        private async Task<(MemoryStream stream, long uncompressedLength, long recordCount)> SerializeDataAsync(IVersionSet versionSet, T data)
        {
            var memoryStream = new MemoryStream();

            long uncompressedLength;
            long recordCount;
            using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                using var countingStream = new CountingWriterStream(gzipStream);
                using var writer = new StreamWriter(countingStream)
                {
                    NewLine = "\n",
                };

                recordCount = await _updater.WriteAsync(versionSet, data, writer);

                await writer.FlushAsync();
                await gzipStream.FlushAsync();

                uncompressedLength = countingStream.Length;
            }

            memoryStream.Position = 0;

            return (memoryStream, uncompressedLength, recordCount);
        }

        private async Task CopyLatestAsync(
            long uncompressedLength,
            long recordCount,
            DateTimeOffset asOfTimestamp,
            DateTimeOffset versionSetCommitTimestamp,
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
                    Metadata = GetMetadata(uncompressedLength, recordCount, asOfTimestamp, versionSetCommitTimestamp),
                });

            await operation.WaitForCompletionAsync();
        }

        private static Dictionary<string, string> GetMetadata(long uncompressedLength, long recordCount, DateTimeOffset asOfTimestamp, DateTimeOffset versionSetCommitTimestamp)
        {
            return new Dictionary<string, string>
            {
                {
                    StorageUtility.RawSizeBytesMetadata,
                    uncompressedLength.ToString(CultureInfo.InvariantCulture)
                },
                {
                    StorageUtility.RecordCountMetadata,
                    recordCount.ToString(CultureInfo.InvariantCulture)
                },
                {
                    VersionSetCommitTimestampMetadata,
                    versionSetCommitTimestamp.ToString("O")
                },
                {
                    AsOfTimestampMetadata,
                    asOfTimestamp.ToString("O")
                },
            };
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
