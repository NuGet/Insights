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

        public async Task CompactAsync<T>(string container, int bucket, Func<IEnumerable<T>, IEnumerable<T>> prune)
        {
            var appendBlob = GetAppendBlob(container, bucket);
            var bytes = Array.Empty<byte>();
            if (await appendBlob.ExistsAsync())
            {
                var appendText = await appendBlob.DownloadTextAsync();
                List<T> allRecords;
                using (var reader = new StringReader(appendText))
                using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csvReader.Configuration.HasHeaderRecord = false;
                    allRecords = csvReader.GetRecords<T>().ToList();
                }

                var prunedRecords = prune(allRecords).ToList();

                bytes = SerializeRecords(prunedRecords);
            }

            var compactBlob = GetCompactlob(container, bucket);
            compactBlob.Properties.ContentType = ContentType;
            await compactBlob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
        }

        public async Task<int> CountCompactBlobsAsync(string container)
        {
            var containerReference = GetContainer(container);
            var blobCount = 0;
            BlobContinuationToken token = null;
            do
            {
                var segment = await containerReference.ListBlobsSegmentedAsync(CompactPrefix, token);
                token = segment.ContinuationToken;
                blobCount += segment.Results.Count();
            }
            while (token != null);
            return blobCount;
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
