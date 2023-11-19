// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using MessagePack;
using Microsoft.Extensions.Logging;
using NuGet.Insights.WideEntities;

namespace NuGet.Insights.Worker
{
    public class AppendResultStorageService
    {
        private static readonly ConcurrentDictionary<Type, string> TypeToHeader = new ConcurrentDictionary<Type, string>();

        private const string ContentType = "text/plain";
        public const string CompactPrefix = "compact_";

        private readonly ServiceClientFactory _serviceClientFactory;
        private readonly WideEntityService _wideEntityService;
        private readonly ICsvReader _csvReader;
        private readonly ITelemetryClient _telemetryClient;
        private readonly ILogger<AppendResultStorageService> _logger;

        public AppendResultStorageService(
            ServiceClientFactory serviceClientFactory,
            WideEntityService wideEntityService,
            ICsvReader csvReader,
            ITelemetryClient telemetryClient,
            ILogger<AppendResultStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _wideEntityService = wideEntityService;
            _csvReader = csvReader;
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        public async Task InitializeAsync(string srcTable, string destContainer)
        {
            await _wideEntityService.CreateTableAsync(srcTable);
            await (await GetContainerAsync(destContainer)).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteAsync(string srcTable)
        {
            await _wideEntityService.DeleteTableAsync(srcTable);
        }

        public async Task AppendAsync(string srcTable, int bucketCount, IEnumerable<ICsvRecordSet<ICsvRecord>> sets)
        {
            var bucketGroups = sets
                .SelectMany(x => x.Records.Select(y => (x.BucketKey, Record: y)))
                .GroupBy(x => StorageUtility.GetBucket(bucketCount, x.BucketKey), x => x.Record);

            foreach (var group in bucketGroups)
            {
                var records = group.ToList();
                foreach (var record in records)
                {
                    record.SetEmptyStrings();
                }

                await AppendAsync(srcTable, group.Key, records);
            }
        }

        private async Task AppendAsync(string srcTable, int bucket, IReadOnlyList<ICsvRecord> records)
        {
            // Append the data.
            await AppendToTableAsync(bucket, srcTable, records);

            // Append a marker to show that this bucket has data.
            try
            {
                await _wideEntityService.InsertAsync(
                   srcTable,
                   partitionKey: string.Empty,
                   rowKey: bucket.ToString(CultureInfo.InvariantCulture),
                   content: Array.Empty<byte>());
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
            {
                // This is okay. The marker already exists.
            }
        }

        public async Task<IReadOnlyList<T>> ReadAsync<T>(string destContainer, int bucket) where T : ICsvRecord
        {
            var compactBlob = await GetCompactBlobAsync(destContainer, bucket);

            try
            {
                (var records, _) = await DeserializeBlobAsync<T>(compactBlob);
                return records;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                return Array.Empty<T>();
            }
        }

        private async Task AppendToTableAsync<T>(int bucket, string srcTable, IReadOnlyList<T> records) where T : ICsvRecord
        {
            var bytes = Serialize(records);
            try
            {
                var attempt = 0;
                while (true)
                {
                    attempt++;
                    var rowKey = StorageUtility.GenerateDescendingId().ToString();
                    try
                    {
                        await _wideEntityService.InsertAsync(srcTable, partitionKey: bucket.ToString(CultureInfo.InvariantCulture), rowKey, content: bytes);
                        break;
                    }
                    catch (RequestFailedException ex) when (attempt < 3 && ex.Status == (int)HttpStatusCode.Conflict)
                    {
                        // These conflicts can occur if there is an internal retry on the insert. Just in case, we'll
                        // insert the data again and allow the in-memory pruning to remove duplicates later.
                        _logger.LogTransientWarning(
                            ex,
                            "Conflict when inserting {Count} CSV records into bucket {Bucket} with row key {RowKey}.",
                            records.Count,
                            bucket,
                            rowKey);
                    }
                }
            }
            catch (RequestFailedException ex) when (
                records.Count >= 2
                && ex.Status == (int)HttpStatusCode.RequestEntityTooLarge
                && ex.ErrorCode == "RequestBodyTooLarge")
            {
                var recordName = typeof(T).FullName;
                GetTooLargeRecordCountMetric().TrackValue(records.Count, recordName);
                GetTooLargeSizeInBytesMetric().TrackValue(bytes.Length, recordName);

                var firstHalf = records.Take(records.Count / 2).ToList();
                var secondHalf = records.Skip(firstHalf.Count).ToList();
                await AppendToTableAsync(bucket, srcTable, firstHalf);
                await AppendToTableAsync(bucket, srcTable, secondHalf);
            }
        }

        public async Task CompactAsync<T>(
            string srcTable,
            string destContainer,
            int bucket,
            bool force,
            Prune<T> prune) where T : ICsvRecord
        {
            var appendRecords = new List<T>();
            const int PruneEveryNEntity = 500;

            var recordType = typeof(T).FullName;
            var stopwatch = Stopwatch.StartNew();

            var entityCount = 0;

            if (!force || srcTable != null)
            {
                var pruneRecordCountMetric = GetPruneRecordCountMetric();
                var pruneRecordDeltaMetric = GetPruneRecordDeltaMetric();
                var entities = _wideEntityService.RetrieveAsync(
                    srcTable,
                    partitionKey: bucket.ToString(CultureInfo.InvariantCulture),
                    minRowKey: null,
                    maxRowKey: null,
                    includeData: true,
                    maxPerPage: StorageUtility.MaxTakeCount);
                await foreach (var entity in entities)
                {
                    entityCount++;
                    using var stream = entity.GetStream();
                    var records = Deserialize<T>(stream);
                    appendRecords.AddRange(records);

                    // Proactively prune to avoid out of memory exceptions.
                    if (entityCount % PruneEveryNEntity == PruneEveryNEntity - 1 && appendRecords.Any())
                    {
                        var initialCount = appendRecords.Count;
                        pruneRecordCountMetric.TrackValue(appendRecords.Count, destContainer, recordType, "false");
                        appendRecords = prune(appendRecords, isFinalPrune: false);
                        pruneRecordDeltaMetric.TrackValue(appendRecords.Count - initialCount, destContainer, recordType, "false");
                    }
                }

                if (!appendRecords.Any() && !force)
                {
                    // If there are no entities, then there's no new data. We can stop here.
                    GetPruneEntityCountMetric().TrackValue(entityCount, destContainer, recordType);
                    return;
                }
            }

            GetPruneEntityCountMetric().TrackValue(entityCount, destContainer, recordType);

            await CompactAsync(appendRecords, destContainer, recordType, bucket, prune);

            GetCompactDurationMsMetric().TrackValue(stopwatch.Elapsed.TotalMilliseconds, destContainer, recordType);
        }

        private IMetric GetTooLargeRecordCountMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(AppendToTableAsync)}.TooLarge.RecordCount",
                "RecordType");
        }

        private IMetric GetTooLargeSizeInBytesMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(AppendToTableAsync)}.TooLarge.SizeInBytes",
                "RecordType");
        }

        private IMetric GetCompactDurationMsMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.DurationMs",
                "DestContainer",
                "RecordType");
        }

        private IMetric GetPruneRecordCountMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.PruneRecordCount",
                "DestContainer",
                "RecordType",
                "IsFinalPrune");
        }

        private IMetric GetPruneRecordDeltaMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.PruneRecordDelta",
                "DestContainer",
                "RecordType",
                "IsFinalPrune");
        }

        private IMetric GetRecordCountMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.RecordCount",
                "DestContainer",
                "RecordType");
        }

        private IMetric GetPruneEntityCountMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.EntityCount",
                "DestContainer",
                "RecordType");
        }

        private IMetric GetCompressedSizeMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.CompressedSizeInBytes",
                "DestContainer",
                "RecordType");
        }

        private IMetric GetUncompressedSizeMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.UncompressedSizeInBytes",
                "DestContainer",
                "RecordType");
        }

        private IMetric GetBlobChangeMetric()
        {
            return _telemetryClient.GetMetric(
                $"{nameof(AppendResultStorageService)}.{nameof(CompactAsync)}.BlobChange",
                "DestContainer",
                "RecordType");
        }

        private async Task CompactAsync<T>(
            List<T> records,
            string destContainer,
            string recordType,
            int bucket,
            Prune<T> prune) where T : ICsvRecord
        {
            var compactBlob = await GetCompactBlobAsync(destContainer, bucket);

            BlobRequestConditions requestConditions;
            BlobDownloadDetails previousDetails;
            try
            {
                (var existingRecords, previousDetails) = await DeserializeBlobAsync<T>(compactBlob);
                records.AddRange(existingRecords);
                requestConditions = new BlobRequestConditions { IfMatch = previousDetails.ETag };
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                previousDetails = null;
                requestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
            }

            if (records.Any())
            {
                var initialCount = records.Count;
                GetPruneRecordCountMetric().TrackValue(records.Count, destContainer, recordType, "true");
                records = prune(records, isFinalPrune: true);
                GetPruneRecordDeltaMetric().TrackValue(records.Count - initialCount, destContainer, recordType, "true");
            }

            GetRecordCountMetric().TrackValue(records.Count, destContainer, recordType);

            using var stream = SerializeRecords(records, writeHeader: true, gzip: true, out var uncompressedLength);

            BlobContentInfo info = await compactBlob.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    Conditions = requestConditions,
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ContentType,
                        ContentEncoding = "gzip",
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        {
                            StorageUtility.RawSizeBytesMetadata,
                            uncompressedLength.ToString(CultureInfo.InvariantCulture)
                        },
                    },
                });

            GetCompressedSizeMetric().TrackValue(stream.Length, destContainer, recordType);
            GetUncompressedSizeMetric().TrackValue(uncompressedLength, destContainer, recordType);
            var changed = previousDetails is null || !previousDetails.ContentHash.SequenceEqual(info.ContentHash);
            GetBlobChangeMetric().TrackValue(changed ? 1 : 0, destContainer, recordType);
        }

        private async Task<(List<T> records, BlobDownloadDetails details)> DeserializeBlobAsync<T>(BlockBlobClient blob) where T : ICsvRecord
        {
            var bufferSize = 32 * 1024;
            do
            {
                (var result, var details) = await DeserializeBlobAsync<T>(blob, bufferSize);
                switch (result.Type)
                {
                    case CsvReaderResultType.Success:
                        return (result.Records, details);

                    case CsvReaderResultType.BufferTooSmall:
                        bufferSize = CsvReaderAdapter.MaxBufferSize;
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
            while (bufferSize <= CsvReaderAdapter.MaxBufferSize);

            throw new InvalidOperationException($"Could not deserialize blob after trying buffers up to {bufferSize} bytes in size.");
        }

        private async Task<(CsvReaderResult<T> result, BlobDownloadDetails details)> DeserializeBlobAsync<T>(BlockBlobClient blob, int bufferSize)
            where T : ICsvRecord
        {
            using BlobDownloadInfo info = await blob.DownloadAsync();
            var readStream = info.Content;
            try
            {
                if (info.Details.ContentEncoding == "gzip")
                {
                    readStream = new GZipStream(readStream, CompressionMode.Decompress);
                }

                using var reader = new StreamReader(readStream);

                var actualHeader = reader.ReadLine();
                var expectedHeader = GetHeader<T>();
                if (actualHeader != expectedHeader)
                {
                    throw new InvalidOperationException(
                        "The header in the blob doesn't match the header for the readers being added." + Environment.NewLine +
                        "Expected: " + expectedHeader + Environment.NewLine +
                        "Actual: " + actualHeader);
                }

                return (_csvReader.GetRecords<T>(reader, bufferSize), info.Details);
            }
            finally
            {
                readStream?.Dispose();
            }
        }

        public async Task<List<int>> GetAppendedBucketsAsync(string srcTable)
        {
            var markerEntities = await _wideEntityService.RetrieveAsync(srcTable, partitionKey: string.Empty);
            return markerEntities.Select(x => int.Parse(x.RowKey, CultureInfo.InvariantCulture)).ToList();
        }

        public async Task<List<int>> GetCompactedBucketsAsync(string destContainer)
        {
            var container = await GetContainerAsync(destContainer);
            var buckets = new List<int>();

            if (!await container.ExistsAsync())
            {
                return buckets;
            }

            var regex = new Regex(Regex.Escape(CompactPrefix) + @"(\d+)");
            var blobs = container.GetBlobsAsync(prefix: CompactPrefix);
            await foreach (var blob in blobs)
            {
                var bucket = int.Parse(regex.Match(blob.Name).Groups[1].Value, CultureInfo.InvariantCulture);
                buckets.Add(bucket);
            }

            return buckets;
        }

        public async Task<Uri> GetCompactedBlobUrlAsync(string destContainer, int bucket)
        {
            var blob = await GetCompactBlobAsync(destContainer, bucket);
            return blob.Uri;
        }

        private async Task<BlockBlobClient> GetCompactBlobAsync(string destContainer, int bucket)
        {
            return (await GetContainerAsync(destContainer)).GetBlockBlobClient($"{CompactPrefix}{bucket}.csv.gz");
        }

        private static byte[] Serialize<T>(IReadOnlyList<T> records) where T : ICsvRecord
        {
            return MessagePackSerializer.Serialize(records, NuGetInsightsMessagePack.Options);
        }

        private static IReadOnlyList<T> Deserialize<T>(Stream stream) where T : ICsvRecord
        {
            return MessagePackSerializer.Deserialize<List<T>>(stream, NuGetInsightsMessagePack.Options);
        }

        private static MemoryStream SerializeRecords<T>(IReadOnlyList<T> records, bool writeHeader, bool gzip, out long uncompressedLength) where T : ICsvRecord
        {
            var memoryStream = new MemoryStream();
            if (gzip)
            {
                using var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal, leaveOpen: true);
                using var countingStream = new CountingWriterStream(gzipStream);
                SerializeRecords(records, countingStream, writeHeader);
                uncompressedLength = countingStream.Length;
            }
            else
            {
                using var countingStream = new CountingWriterStream(memoryStream);
                SerializeRecords(records, countingStream, writeHeader);
                uncompressedLength = countingStream.Length;
            }

            memoryStream.Position = 0;
            return memoryStream;
        }

        private static void SerializeRecords<T>(IReadOnlyList<T> records, Stream destination, bool writeHeader) where T : ICsvRecord
        {
            using var streamWriter = new StreamWriter(destination, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true)
            {
                NewLine = "\n",
            };

            if (writeHeader)
            {
                streamWriter.WriteLine(GetHeader<T>());
            }

            SerializeRecords(records, streamWriter);
        }

        private static void SerializeRecords<T>(IReadOnlyList<T> records, TextWriter streamWriter) where T : ICsvRecord
        {
            foreach (var record in records)
            {
                record.Write(streamWriter);
            }
        }

        private async Task<BlobContainerClient> GetContainerAsync(string name)
        {
            var serviceClient = await _serviceClientFactory.GetBlobServiceClientAsync();
            return serviceClient.GetBlobContainerClient(name);
        }

        private static string GetHeader<T>() where T : ICsvRecord
        {
            var type = typeof(T);
            return TypeToHeader.GetOrAdd(type, _ =>
            {
                var headerWriter = Activator.CreateInstance<T>();
                using var stringWriter = new StringWriter();
                headerWriter.WriteHeader(stringWriter);
                return stringWriter.ToString().TrimEnd();
            });
        }
    }
}
