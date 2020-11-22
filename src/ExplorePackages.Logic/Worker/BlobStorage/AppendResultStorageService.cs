using CsvHelper;
using CsvHelper.TypeConversion;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.BlobStorage
{
    public class AppendResultStorageService
    {
        private const string ContentType = "text/plain";
        private const string AppendPrefix = "append_";
        private const string CompactPrefix = "compact_";

        private readonly ServiceClientFactory _serviceClientFactory;

        public AppendResultStorageService(ServiceClientFactory serviceClientFactory)
        {
            _serviceClientFactory = serviceClientFactory;
        }

        public async Task InitializeAsync(string container)
        {
            await GetContainer(container).CreateIfNotExistsAsync(retry: true);
        }

        public async Task DeleteAsync(string container)
        {
            await GetContainer(container).DeleteIfExistsAsync();
        }

        public async Task AppendAsync<T>(AppendResultStorage storage, string bucketKey, IReadOnlyList<T> records)
        {
            var bucket = GetBucket(storage.BucketCount, bucketKey);
            var blob = GetAppendBlob(storage.Container, bucket);
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

            await AppendAsync(blob, records);
        }

        private CloudAppendBlob GetAppendBlob(string container, int bucket)
        {
            return GetContainer(container).GetAppendBlobReference($"{AppendPrefix}{bucket}.csv");
        }

        private CloudBlockBlob GetCompactlob(string container, int bucket)
        {
            return GetContainer(container).GetBlockBlobReference($"{CompactPrefix}{bucket}.csv");
        }

        public async Task CompactAsync<T>(
            string srcContainer,
            string destContainer,
            int bucket,
            bool mergeExisting,
            Func<IEnumerable<T>, IEnumerable<T>> prune)
        {
            var appendBlob = GetAppendBlob(srcContainer, bucket);
            var compactBlob = GetCompactlob(destContainer, bucket);

            var allRecords = new List<T>();
            if (await appendBlob.ExistsAsync())
            {
                var text = await appendBlob.DownloadTextAsync();
                var records = DeserializeRecords<T>(text);
                allRecords.AddRange(records);
            }
            else
            {
                // If there is no append blob, then there's no new data. We can stop here.
                return;
            }

            if (mergeExisting && await compactBlob.ExistsAsync())
            {
                var text = await compactBlob.DownloadTextAsync();
                var records = DeserializeRecords<T>(text);
                allRecords.AddRange(records);
            }

            var bytes = Array.Empty<byte>();
            if (allRecords.Any())
            {
                var prunedRecords = prune(allRecords).ToList();
                bytes = SerializeRecords(prunedRecords);
            }

            compactBlob.Properties.ContentType = ContentType;
            await compactBlob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
        }

        private static List<T> DeserializeRecords<T>(string appendText)
        {
            List<T> allRecords;
            using (var reader = new StringReader(appendText))
            using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csvReader.Configuration.HasHeaderRecord = false;
                allRecords = csvReader.GetRecords<T>().ToList();
            }

            return allRecords;
        }

        public async Task<List<int>> GetWrittenAppendBuckets(string container)
        {
            var containerReference = GetContainer(container);
            var buckets = new List<int>();
            BlobContinuationToken token = null;
            do
            {
                var segment = await containerReference.ListBlobsSegmentedAsync(AppendPrefix, token);
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

        private async Task AppendAsync<T>(CloudAppendBlob blob, IReadOnlyList<T> records)
        {
            using var memoryStream = new MemoryStream(SerializeRecords(records));
            try
            {
                await blob.AppendBlockAsync(memoryStream);
            }
            catch (StorageException ex) when (
                records.Count >= 2
                && ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.RequestEntityTooLarge)
            {
                var firstHalf = records.Take(records.Count / 2).ToList();
                var secondHalf = records.Skip(records.Count - firstHalf.Count).ToList();
                await AppendAsync(blob, firstHalf);
                await AppendAsync(blob, secondHalf);
            }
        }

        private static byte[] SerializeRecords<T>(IReadOnlyList<T> records)
        {
            using var writeMemoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(writeMemoryStream, new UTF8Encoding(false)))
            using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                var options = new TypeConverterOptions { Formats = new[] { "O" } };
                csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<DateTimeOffset>(options);
                csvWriter.Configuration.HasHeaderRecord = false;
                csvWriter.WriteRecords(records);
            }

            return writeMemoryStream.ToArray();
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

        private CloudBlobContainer GetContainer(string name)
        {
            var storageAccount = _serviceClientFactory.GetStorageAccount();
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference(name);
            return container;
        }
    }
}
