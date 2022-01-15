// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
        private readonly ILogger<AppendResultStorageService> _logger;

        public AppendResultStorageService(
            ServiceClientFactory serviceClientFactory,
            WideEntityService wideEntityService,
            ICsvReader csvReader,
            ILogger<AppendResultStorageService> logger)
        {
            _serviceClientFactory = serviceClientFactory;
            _wideEntityService = wideEntityService;
            _csvReader = csvReader;
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
                .GroupBy(x => GetBucket(bucketCount, x.BucketKey), x => x.Record);

            foreach (var group in bucketGroups)
            {
                await AppendAsync(srcTable, group.Key, group.ToList());
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
                   rowKey: bucket.ToString(),
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

        private async Task AppendToTableAsync(int bucket, string srcTable, IReadOnlyList<ICsvRecord> records)
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
                        await _wideEntityService.InsertAsync(srcTable, partitionKey: bucket.ToString(), rowKey, content: bytes);
                        break;
                    }
                    catch (RequestFailedException ex) when (attempt < 3 && ex.Status == (int)HttpStatusCode.Conflict)
                    {
                        // I've seen some conflicts on this insert before, shockingly! I don't believe in GUID
                        // collisions! But let's try again with a new ID just in case.
                        _logger.LogWarning(
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
            Func<List<T>, List<T>> prune) where T : ICsvRecord
        {
            var appendRecords = new List<T>();

            if (!force || srcTable != null)
            {
                var entities = await _wideEntityService.RetrieveAsync(srcTable, partitionKey: bucket.ToString());

                if (entities.Any())
                {
                    foreach (var entity in entities)
                    {
                        using var stream = entity.GetStream();
                        var records = Deserialize<T>(stream);
                        appendRecords.AddRange(records);
                    }
                }
                else if (!force)
                {
                    // If there are no entities, then there's no new data. We can stop here.
                    return;
                }
            }

            await CompactAsync(appendRecords, destContainer, bucket, prune);
        }

        private async Task CompactAsync<T>(
            List<T> records,
            string destContainer,
            int bucket,
            Func<List<T>, List<T>> prune) where T : ICsvRecord
        {
            var compactBlob = await GetCompactBlobAsync(destContainer, bucket);

            BlobRequestConditions requestConditions;
            try
            {
                (var existingRecords, var etag) = await DeserializeBlobAsync<T>(compactBlob);
                records.AddRange(existingRecords);
                requestConditions = new BlobRequestConditions { IfMatch = etag };
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                requestConditions = new BlobRequestConditions { IfNoneMatch = ETag.All };
            }

            if (records.Any())
            {
                records = prune(records);
            }

            using var stream = SerializeRecords(records, writeHeader: true, gzip: true, out var uncompressedLength);

            await compactBlob.UploadAsync(
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
                            uncompressedLength.ToString()
                        },
                    },
                });
        }

        private async Task<(List<T> records, ETag etag)> DeserializeBlobAsync<T>(BlobClient blob) where T : ICsvRecord
        {
            var bufferSize = 32 * 1024;
            do
            {
                (var result, var etag) = await DeserializeBlobAsync<T>(blob, bufferSize);
                switch (result.Type)
                {
                    case CsvReaderResultType.Success:
                        return (result.Records, etag);

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

        private async Task<(CsvReaderResult<T> result, ETag etag)> DeserializeBlobAsync<T>(BlobClient blob, int bufferSize)
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

                return (_csvReader.GetRecords<T>(reader, bufferSize), info.Details.ETag);
            }
            finally
            {
                readStream?.Dispose();
            }
        }

        public async Task<List<int>> GetAppendedBucketsAsync(string srcTable)
        {
            var markerEntities = await _wideEntityService.RetrieveAsync(srcTable, partitionKey: string.Empty);
            return markerEntities.Select(x => int.Parse(x.RowKey)).ToList();
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
                var bucket = int.Parse(regex.Match(blob.Name).Groups[1].Value);
                buckets.Add(bucket);
            }

            return buckets;
        }

        public async Task<Uri> GetCompactedBlobUrlAsync(string destContainer, int bucket)
        {
            var blob = await GetCompactBlobAsync(destContainer, bucket);
            return blob.Uri;
        }

        private async Task<BlobClient> GetCompactBlobAsync(string destContainer, int bucket)
        {
            return (await GetContainerAsync(destContainer)).GetBlobClient($"{CompactPrefix}{bucket}.csv.gz");
        }

        private static byte[] Serialize(IReadOnlyList<ICsvRecord> records)
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

        public static int GetBucket(int bucketCount, string bucketKey)
        {
            int bucket;
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(bucketKey));
                bucket = (int)(BitConverter.ToUInt64(hash) % (ulong)bucketCount);
            }

            return bucket;
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
