using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Shared.Protocol;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage.Table.Protocol;

namespace Knapcode.ExplorePackages.Worker
{
    public class AppendResultStorageService
    {
        private const string ContentType = "text/plain";
        private const string AppendPrefix = "append_";
        private const string CompactPrefix = "compact_";
        private const int MaximumPropertyLength = 32 * 1024;
        private readonly IServiceClientFactory _serviceClientFactory;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<ExplorePackagesWorkerSettings> _options;

        public AppendResultStorageService(
            IServiceClientFactory serviceClientFactory,
            ITelemetryClient telemetryClient,
            IOptions<ExplorePackagesWorkerSettings> options)
        {
            _serviceClientFactory = serviceClientFactory;
            _telemetryClient = telemetryClient;
            _options = options;
        }

        public async Task InitializeAsync(string srcContainer, string destContainer)
        {
            switch (_options.Value.AppendResultStorageMode)
            {
                case AppendResultStorageMode.Table:
                    await GetTable(srcContainer).CreateIfNotExistsAsync(retry: true);
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
                    await GetTable(containerName).DeleteIfExistsAsync();
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
                try
                {
                    await blob.CreateOrReplaceAsync(
                        accessCondition: AccessCondition.GenerateIfNotExistsCondition(),
                        options: null,
                        operationContext: null);
                }
                catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.Conflict)
                {
                    // Ignore this exception.
                }

                // Best effort, will not be re-executed on retry since the blob will exist at that point.
                blob.Properties.ContentType = ContentType;
                await blob.SetPropertiesAsync();
            }

            await AppendToBlobAsync(blob, records);
        }

        private async Task AppendToBlobAsync<T>(CloudAppendBlob blob, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            using var memoryStream = SerializeRecords(records);
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
            var table = GetTable(tableName);

            // Append the data.
            await AppendToTableAsync(bucket, table, records);

            // Append a marker to show that this bucket has data.
            var markerEntity = new TableEntity(string.Empty, bucket.ToString());
            await table.ExecuteAsync(TableOperation.InsertOrReplace(markerEntity));
        }

        private static async Task AppendToTableAsync<T>(int bucket, ICloudTable table, IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            AppendResultEntity dataEntity = SerializeToTableEntity(records, bucket);

            var entities = new List<AppendResultEntity>();
            if (dataEntity.Data.Length >= MaximumPropertyLength)
            {
                var entityCount = Math.Min(
                    records.Count,
                    (int)(1.5 * ((dataEntity.Data.Length / MaximumPropertyLength) + 1)));

                if (entityCount > 1)
                {
                    var minRecordCount = records.Count / entityCount;
                    for (var i = 0; i < entityCount; i++)
                    {
                        var entityRecords = records.Skip(i * minRecordCount);
                        if (i < entityCount - 1)
                        {
                            entityRecords = entityRecords.Take(minRecordCount);
                        }

                        entities.Add(SerializeToTableEntity(entityRecords.ToList(), bucket));
                    }
                }
            }

            if (!entities.Any())
            {
                entities.Add(dataEntity);
            }

            try
            {
                await table.InsertEntitiesAsync(entities);
            }
            catch (StorageException ex) when (
                records.Count >= 2 && (
                    (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.BadRequest
                     && (ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == TableErrorCodeStrings.PropertyValueTooLarge
                         || ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == TableErrorCodeStrings.EntityTooLarge))
                    ||
                    (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.RequestEntityTooLarge
                     && ex.RequestInformation?.ExtendedErrorInformation?.ErrorCode == StorageErrorCodeStrings.RequestBodyTooLarge)))
            {
                var firstHalf = records.Take(records.Count / 2).ToList();
                var secondHalf = records.Skip(firstHalf.Count).ToList();
                await AppendToTableAsync(bucket, table, firstHalf);
                await AppendToTableAsync(bucket, table, secondHalf);
            }
        }

        private static AppendResultEntity SerializeToTableEntity<T>(IReadOnlyList<T> records, int bucket) where T : ICsvRecord<T>, new()
        {
            using var stringWriter = new StringWriter();
            SerializeRecords(records, stringWriter);

            return new AppendResultEntity(bucket, StorageUtility.GenerateDescendingId().ToString())
            {
                Data = stringWriter.ToString(),
            };
        }

        private ICloudBlobWrapper GetBlob(string container, string prefix, int bucket)
        {
            return GetContainer(container).GetBlobReference($"{prefix}{bucket}.csv");
        }

        private CloudAppendBlob GetAppendBlob(string container, int bucket)
        {
            return GetContainer(container).GetAppendBlobReference($"{AppendPrefix}{bucket}.csv");
        }

        private ICloudBlockBlobWrapper GetCompactBlob(string container, int bucket)
        {
            return GetContainer(container).GetBlockBlobReference($"{CompactPrefix}{bucket}.csv");
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
                var blob = GetBlob(srcContainer, AppendPrefix, bucket);
                if (await blob.ExistsAsync())
                {
                    var records = await DeserializeBlobAsync<T>(blob, csvReader);
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
                var table = GetTable(srcTable);
                var entities = await table.GetEntitiesAsync<AppendResultEntity>(bucket.ToString(), _telemetryClient.NewQueryLoopMetrics());
                if (entities.Any())
                {
                    foreach (var entity in entities)
                    {
                        using var stringReader = new StringReader(entity.Data);
                        appendRecords.AddRange(csvReader.GetRecords<T>(stringReader));
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
                var records = await DeserializeBlobAsync<T>(compactBlob, csvReader);
                allRecords.AddRange(records);
                accessCondition = AccessCondition.GenerateIfMatchCondition(compactBlob.Properties.ETag);
            }

            Stream stream = Stream.Null;
            if (allRecords.Any())
            {
                var prunedRecords = prune(allRecords);
                stream = SerializeRecords(prunedRecords);
            }

            compactBlob.Properties.ContentType = ContentType;
            stream.Position = 0;
            await compactBlob.UploadFromStreamAsync(stream, accessCondition, options: null, operationContext: null);
        }

        private static async Task<List<T>> DeserializeBlobAsync<T>(ICloudBlobWrapper blob, ICsvReader csvReader) where T : ICsvRecord<T>, new()
        {
            using (var memoryStream = new MemoryStream())
            {
                await blob.DownloadToStreamAsync(memoryStream);
                memoryStream.Position = 0;
                using var reader = new StreamReader(memoryStream);
                return csvReader.GetRecords<T>(reader);
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
            var table = GetTable(tableName);
            var markerEntities = await table.GetEntitiesAsync<TableEntity>(string.Empty, _telemetryClient.NewQueryLoopMetrics());
            return markerEntities.Select(x => int.Parse(x.RowKey)).ToList();
        }

        private static MemoryStream SerializeRecords<T>(IReadOnlyList<T> records) where T : ICsvRecord<T>, new()
        {
            var memoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(memoryStream, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
            {
                SerializeRecords(records, streamWriter);
            }

            return memoryStream;
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

        private ICloudTable GetTable(string name)
        {
            var storageAccount = _serviceClientFactory.GetAbstractedStorageAccount();
            var client = storageAccount.CreateCloudTableClient();
            return client.GetTableReference(name);
        }

        private ICloudBlobContainer GetContainer(string name)
        {
            var storageAccount = _serviceClientFactory.GetAbstractedStorageAccount();
            var client = storageAccount.CreateCloudBlobClient();
            return client.GetContainerReference(name);
        }
    }
}
