using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.WideEntities;
using MessagePack;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;

namespace Knapcode.ExplorePackages.Worker
{
    public class AppendResultStorageService
    {
        private static readonly ConcurrentDictionary<Type, string> TypeToHeader = new ConcurrentDictionary<Type, string>();

        private const string ContentType = "text/plain";
        private const string AppendPrefix = "append_";
        private const string CompactPrefix = "compact_";
        private readonly IServiceClientFactory _serviceClientFactory;
        private readonly WideEntityService _wideEntityService;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public AppendResultStorageService(
            IServiceClientFactory serviceClientFactory,
            WideEntityService wideEntityService,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _wideEntityService = wideEntityService;
            _options = options;
        }

        public async Task InitializeAsync(string srcContainer, string destContainer)
        {
            switch (_options.Value.AppendResultStorageMode)
            {
                case AppendResultStorageMode.Table:
                    await _wideEntityService.CreateTableAsync(srcContainer);
                    break;
                case AppendResultStorageMode.AppendBlob:
                    await GetContainer(srcContainer).CreateIfNotExistsAsync(retry: true);
                    break;
                default:
                    throw new NotImplementedException();
            }

            await GetContainer(destContainer).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteAsync(string containerName)
        {
            switch (_options.Value.AppendResultStorageMode)
            {
                case AppendResultStorageMode.Table:
                    await _wideEntityService.DeleteTableAsync(containerName);
                    break;
                case AppendResultStorageMode.AppendBlob:
                    await GetContainer(containerName).DeleteIfExistsAsync();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public async Task AppendAsync<T>(string containerName, int bucketCount, string bucketKey, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            switch (_options.Value.AppendResultStorageMode)
            {
                case AppendResultStorageMode.Table:
                    await AppendToTableAsync(containerName, bucketCount, bucketKey, records);
                    break;
                case AppendResultStorageMode.AppendBlob:
                    await AppendToBlobAsync(containerName, bucketCount, bucketKey, records);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private async Task AppendToBlobAsync<T>(string containerName, int bucketCount, string bucketKey, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            var bucket = GetBucket(bucketCount, bucketKey);
            var blob = GetAppendBlob(containerName, bucket);
            if (!await blob.ExistsAsync())
            {
                AccessCondition accessCondition;
                try
                {
                    await blob.CreateOrReplaceAsync(
                        accessCondition: AccessCondition.GenerateIfNotExistsCondition(),
                        options: null,
                        operationContext: null);
                    accessCondition = AccessCondition.GenerateIfMatchCondition(blob.Properties.ETag);

                    blob.Properties.ContentType = ContentType;
                    await blob.AppendTextAsync(GetHeader<T>() + Environment.NewLine, new UTF8Encoding(false), accessCondition, options: null, operationContext: null);
                }
                catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict
                                               || ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                {
                    // Best effort, will not be re-executed on retry since the blob will exist at that point. If this code
                    // path was used more we do could some optimistic concurrency stuff to gate on the first line (header)
                    // and content type being set, but that's too much work.
                }
            }

            await AppendToBlobAsync(blob, records);
        }

        private async Task AppendToBlobAsync<T>(CloudAppendBlob blob, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            using var memoryStream = SerializeRecords(records, writeHeader: false, gzip: false, out var _);
            try
            {
                await blob.AppendBlockAsync(memoryStream);
            }
            catch (StorageException ex) when (
                records.Count >= 2
                && ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.RequestEntityTooLarge)
            {
                var firstHalf = records.Take(records.Count / 2).ToList();
                var secondHalf = records.Skip(firstHalf.Count).ToList();
                await AppendToBlobAsync(blob, firstHalf);
                await AppendToBlobAsync(blob, secondHalf);
            }
        }

        private async Task AppendToTableAsync<T>(string tableName, int bucketCount, string bucketKey, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            var bucket = GetBucket(bucketCount, bucketKey);

            // Append the data.
            await AppendToTableAsync(bucket, tableName, records);

            // Append a marker to show that this bucket has data.
            try
            {
                await _wideEntityService.InsertAsync(
                   tableName,
                   partitionKey: string.Empty,
                   rowKey: bucket.ToString(),
                   content: Array.Empty<byte>());
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
            {
                // This is okay. The marker already exists.
            }
        }

        private async Task AppendToTableAsync<T>(int bucket, string tableName, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            var bytes = Serialize(records);
            try
            {
                await _wideEntityService.InsertAsync(
                    tableName,
                    partitionKey: bucket.ToString(),
                    rowKey: StorageUtility.GenerateDescendingId().ToString(),
                    content: bytes);
            }
            catch (StorageException ex) when (
                records.Count >= 2
                && ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.RequestEntityTooLarge
                && ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == StorageErrorCodeStrings.RequestBodyTooLarge)
            {
                var firstHalf = records.Take(records.Count / 2).ToList();
                var secondHalf = records.Skip(firstHalf.Count).ToList();
                await AppendToTableAsync(bucket, tableName, firstHalf);
                await AppendToTableAsync(bucket, tableName, secondHalf);
            }
        }

        private ICloudBlobWrapper GetAppendBlobWrapper(string container, int bucket)
        {
            return GetContainer(container).GetBlobReference($"{AppendPrefix}{bucket}.csv");
        }

        private CloudAppendBlob GetAppendBlob(string container, int bucket)
        {
            return GetContainer(container).GetAppendBlobReference($"{AppendPrefix}{bucket}.csv");
        }

        private ICloudBlockBlobWrapper GetCompactBlob(string container, int bucket)
        {
            return GetContainer(container).GetBlockBlobReference($"{CompactPrefix}{bucket}.csv.gz");
        }

        public async Task CompactAsync<T>(
            string srcContainer,
            string destContainer,
            int bucket,
            bool force,
            bool mergeExisting,
            Func<List<T>, List<T>> prune,
            ICsvReader csvReader) where T : ICsvRecord<T>, new()
        {
            switch (_options.Value.AppendResultStorageMode)
            {
                case AppendResultStorageMode.Table:
                    await CompactFromTableAsync(srcContainer, destContainer, bucket, force, mergeExisting, prune, csvReader);
                    break;
                case AppendResultStorageMode.AppendBlob:
                    await CompactFromBlobAsync(srcContainer, destContainer, bucket, force, mergeExisting, prune, csvReader);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private async Task CompactFromBlobAsync<T>(
            string srcContainer,
            string destContainer,
            int bucket,
            bool force,
            bool mergeExisting,
            Func<List<T>, List<T>> prune,
            ICsvReader csvReader) where T : ICsvRecord<T>, new()
        {
            var appendRecords = new List<T>();

            if (!force || srcContainer != null)
            {
                var blob = GetAppendBlobWrapper(srcContainer, bucket);
                if (await blob.ExistsAsync())
                {
                    var records = await DeserializeBlobAsync<T>(blob, csvReader, gzip: false);
                    appendRecords.AddRange(records);
                }
                else if (!force)
                {
                    // If there is no append blob, then there's no new data. We can stop here.
                    return;
                }
            }

            await CompactAsync(appendRecords, destContainer, bucket, mergeExisting, prune, csvReader);
        }

        private async Task CompactFromTableAsync<T>(
            string srcTable,
            string destContainer,
            int bucket,
            bool force,
            bool mergeExisting,
            Func<List<T>, List<T>> prune,
            ICsvReader csvReader) where T : ICsvRecord<T>, new()
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

            await CompactAsync(appendRecords, destContainer, bucket, mergeExisting, prune, csvReader);
        }

        private static byte[] Serialize<T>(IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            return MessagePackSerializer.Serialize(records, ExplorePackagesMessagePack.Options);
        }

        private static IReadOnlyList<T> Deserialize<T>(Stream stream) where T : ICsvRecord<T>, new()
        {
            return MessagePackSerializer.Deserialize<List<T>>(stream, ExplorePackagesMessagePack.Options);
        }

        private async Task CompactAsync<T>(
            List<T> appendRecords,
            string destContainer,
            int bucket,
            bool mergeExisting,
            Func<List<T>, List<T>> prune,
            ICsvReader csvReader) where T : ICsvRecord<T>, new()
        {
            var allRecords = new List<T>(appendRecords);

            var compactBlob = GetCompactBlob(destContainer, bucket);
            var accessCondition = AccessCondition.GenerateIfNotExistsCondition();
            if (mergeExisting && await compactBlob.ExistsAsync())
            {
                var records = await DeserializeBlobAsync<T>(compactBlob, csvReader, compactBlob.Properties.ContentEncoding == "gzip");
                allRecords.AddRange(records);
                accessCondition = AccessCondition.GenerateIfMatchCondition(compactBlob.Properties.ETag);
            }

            var stream = Stream.Null;
            long uncompressedLength = 0;
            if (allRecords.Any())
            {
                var prunedRecords = prune(allRecords);
                stream = SerializeRecords(prunedRecords, writeHeader: true, gzip: true, out uncompressedLength);
            }

            compactBlob.Properties.ContentType = ContentType;
            compactBlob.Properties.ContentEncoding = "gzip";
            compactBlob.Metadata["rawSizeBytes"] = uncompressedLength.ToString(); // See: https://docs.microsoft.com/en-us/azure/data-explorer/lightingest#recommendations
            await compactBlob.UploadFromStreamAsync(stream, accessCondition, options: null, operationContext: null);
        }

        private static async Task<List<T>> DeserializeBlobAsync<T>(ICloudBlobWrapper blob, ICsvReader csvReader, bool gzip) where T : ICsvRecord<T>, new()
        {
            var bufferSize = 32 * 1024;
            do
            {
                var result = await DeserializeBlobAsync<T>(blob, csvReader, gzip, bufferSize);
                if (result.Type == CsvReaderResultType.Success)
                {
                    return result.Records;
                }

                bufferSize *= 2;
            }
            while (bufferSize <= NRecoCsvReader.MaxBufferSize);

            throw new InvalidOperationException($"Could not deserialize blob after trying buffers up to {bufferSize} bytes in size.");
        }

        private static async Task<CsvReaderResult<T>> DeserializeBlobAsync<T>(ICloudBlobWrapper blob, ICsvReader csvReader, bool gzip, int bufferSize)
            where T : ICsvRecord<T>, new()
        {
            using var blobStream = await blob.OpenReadAsync();
            var readStream = blobStream;
            try
            {
                if (gzip)
                {
                    readStream = new GZipStream(blobStream, CompressionMode.Decompress);
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

                return csvReader.GetRecords<T>(reader, bufferSize);
            }
            finally
            {
                readStream?.Dispose();
            }
        }

        public async Task<List<int>> GetWrittenBucketsAsync(string containerName)
        {
            switch (_options.Value.AppendResultStorageMode)
            {
                case AppendResultStorageMode.Table:
                    return await GetWrittenAppendTableBucketsAsync(containerName);
                case AppendResultStorageMode.AppendBlob:
                    return await GetWrittenAppendBlobBucketsAsync(containerName);
                default:
                    throw new NotImplementedException();
            }
        }

        private async Task<List<int>> GetWrittenAppendBlobBucketsAsync(string containerName)
        {
            var container = GetContainer(containerName);
            var buckets = new List<int>();
            BlobContinuationToken token = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(AppendPrefix, token);
                token = segment.ContinuationToken;
                var segmentBuckets = segment
                    .Results
                    .OfType<ICloudBlob>()
                    .Select(x => Path.GetFileNameWithoutExtension(x.Name))
                    .Select(x => x.Substring(x.LastIndexOf('_') + 1))
                    .Select(int.Parse);
                buckets.AddRange(segmentBuckets);
            }
            while (token != null);

            return buckets;
        }

        private async Task<List<int>> GetWrittenAppendTableBucketsAsync(string tableName)
        {
            var markerEntities = await _wideEntityService.RetrieveAsync(tableName, partitionKey: string.Empty);
            return markerEntities.Select(x => int.Parse(x.RowKey)).ToList();
        }

        private static MemoryStream SerializeRecords<T>(IReadOnlyList<T> records, bool writeHeader, bool gzip, out long uncompressedLength) where T : ICsvRecord<T>, new()
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

        private static void SerializeRecords<T>(IReadOnlyList<T> records, Stream destination, bool writeHeader) where T : ICsvRecord<T>, new()
        {
            using var streamWriter = new StreamWriter(destination, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true);

            if (writeHeader)
            {
                streamWriter.WriteLine(GetHeader<T>());
            }

            SerializeRecords(records, streamWriter);
        }

        private static void SerializeRecords<T>(IReadOnlyList<T> records, TextWriter streamWriter) where T : ICsvRecord<T>, new()
        {
            foreach (var record in records)
            {
                record.Write(streamWriter);
            }
        }

        private static int GetBucket(int bucketCount, string bucketKey)
        {
            int bucket;
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(bucketKey));
                bucket = (int)(BitConverter.ToUInt64(hash) % (ulong)bucketCount);
            }

            return bucket;
        }

        private ICloudBlobContainer GetContainer(string name)
        {
            var storageAccount = _serviceClientFactory.GetAbstractedStorageAccount();
            var client = storageAccount.CreateCloudBlobClient();
            return client.GetContainerReference(name);
        }

        private static string GetHeader<T>() where T : ICsvRecord<T>, new()
        {
            var type = typeof(T);
            return TypeToHeader.GetOrAdd(type, _ =>
            {
                var headerWriter = new T();
                using var stringWriter = new StringWriter();
                headerWriter.WriteHeader(stringWriter);
                return stringWriter.ToString().TrimEnd();
            });
        }
    }
}
