using CsvHelper;
using CsvHelper.TypeConversion;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets
{
    public class FindPackageAssetsStorageService
    {
        private const string ContentType = "text/plain";
        private const string AppendPrefix = "append_";
        private const string CompactPrefix = "compact_";

        private readonly ServiceClientFactory _serviceClientFactory;

        public FindPackageAssetsStorageService(ServiceClientFactory serviceClientFactory)
        {
            _serviceClientFactory = serviceClientFactory;
        }

        public async Task InitializeAsync()
        {
            await GetContainer().CreateIfNotExistsAsync(retry: true);
        }

        public async Task AppendAsync(FindPackageAssetsParameters parameters, string id, string version, List<PackageAsset> assets)
        {
            var bucket = GetBucket(parameters.BucketCount, id, version);
            var blob = GetAppendBlob(bucket);
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

            await AppendAsync(blob, assets);
        }

        private CloudAppendBlob GetAppendBlob(int bucket)
        {
            return GetContainer().GetAppendBlobReference($"{AppendPrefix}{bucket}.csv");
        }

        private CloudBlockBlob GetCompactlob(int bucket)
        {
            return GetContainer().GetBlockBlobReference($"{CompactPrefix}{bucket}.csv");
        }

        public async Task CompactAsync(int bucket)
        {
            var appendBlob = GetAppendBlob(bucket);
            var bytes = Array.Empty<byte>();
            if (await appendBlob.ExistsAsync())
            {
                var appendText = await appendBlob.DownloadTextAsync();
                List<PackageAsset> allAssets;
                using (var reader = new StringReader(appendText))
                using (var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    csvReader.Configuration.HasHeaderRecord = false;
                    allAssets = csvReader.GetRecords<PackageAsset>().ToList();
                }

                var prunedAssets = allAssets
                    .GroupBy(x => new { Id = x.Id.ToLowerInvariant(), Version = x.Version.ToLowerInvariant() }) // Group by unique package version
                    .Select(g => g
                        .GroupBy(x => x.ScanId) // Group package version assets by scan
                        .OrderByDescending(x => x.First().ScanTimestamp) // Ignore all but the most recent scan
                        .First())
                    .SelectMany(g => g)
                    .ToList();

                bytes = SerializeAssets(prunedAssets);
            }

            var compactBlob = GetCompactlob(bucket);
            compactBlob.Properties.ContentType = ContentType;
            await compactBlob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);
        }

        public async Task<int> CountCompactBlobsAsync()
        {
            var container = GetContainer();
            var blobCount = 0;
            BlobContinuationToken token = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync(CompactPrefix, token);
                token = segment.ContinuationToken;
                blobCount += segment.Results.Count();
            }
            while (token != null);
            return blobCount;
        }

        private async Task AppendAsync(CloudAppendBlob blob, List<PackageAsset> assets)
        {
            using var memoryStream = new MemoryStream(SerializeAssets(assets));
            try
            {
                await blob.AppendBlockAsync(memoryStream);
            }
            catch (StorageException ex) when (
                assets.Count >= 2
                && ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.RequestEntityTooLarge)
            {
                var firstHalf = assets.Take(assets.Count / 2).ToList();
                var secondHalf = assets.Skip(assets.Count - firstHalf.Count).ToList();
                await AppendAsync(blob, firstHalf);
                await AppendAsync(blob, secondHalf);
            }
        }

        private static byte[] SerializeAssets(List<PackageAsset> assets)
        {
            using var writeMemoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(writeMemoryStream, new UTF8Encoding(false)))
            using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                var options = new TypeConverterOptions { Formats = new[] { "O" } };
                csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<DateTimeOffset>(options);
                csvWriter.Configuration.HasHeaderRecord = false;
                csvWriter.WriteRecords(assets);
            }

            return writeMemoryStream.ToArray();
        }

        private static int GetBucket(int bucketCount, string id, string version)
        {
            var lowerId = id.ToLowerInvariant();
            var lowerVersion = NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant();

            int bucket;
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes($"{lowerId}/{lowerVersion}"));
                bucket = (int)(BitConverter.ToUInt64(hash) % (ulong)bucketCount);
            }

            return bucket;
        }

        private CloudBlobContainer GetContainer()
        {
            var storageAccount = _serviceClientFactory.GetStorageAccount();
            var client = storageAccount.CreateCloudBlobClient();
            var container = client.GetContainerReference("findpackageassets");
            return container;
        }
    }
}
