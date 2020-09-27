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
            return GetContainer().GetAppendBlobReference($"append/{bucket}.csv");
        }

        private CloudBlockBlob GetCompactlob(int bucket)
        {
            return GetContainer().GetBlockBlobReference($"compact/{bucket}.csv");
        }

        public async Task CompactAsync(int bucket)
        {
            var appendBlob = GetAppendBlob(bucket);
            var builder = new StringBuilder();
            if (await appendBlob.ExistsAsync())
            {
                var appendText = await appendBlob.DownloadTextAsync();
                using var reader = new StringReader(appendText);
                var lines = new HashSet<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (lines.Add(line))
                    {
                        builder.AppendLine(line);
                    }
                }
            }

            var compactBlob = GetCompactlob(bucket);
            compactBlob.Properties.ContentType = ContentType;
            await compactBlob.UploadTextAsync(builder.ToString());
        }

        public async Task<int> CountCompactBlobsAsync()
        {
            var container = GetContainer();
            var blobCount = 0;
            BlobContinuationToken token = null;
            do
            {
                var segment = await container.ListBlobsSegmentedAsync($"compact", token);
                token = segment.ContinuationToken;
                blobCount += segment.Results.Count();
            }
            while (token != null);
            return blobCount;
        }

        private async Task AppendAsync(CloudAppendBlob blob, List<PackageAsset> assets)
        {
            using var writeMemoryStream = new MemoryStream();
            using (var streamWriter = new StreamWriter(writeMemoryStream, Encoding.UTF8))
            using (var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture))
            {
                var options = new TypeConverterOptions { Formats = new[] { "O" } };
                csvWriter.Configuration.TypeConverterOptionsCache.AddOptions<DateTimeOffset>(options);
                csvWriter.Configuration.HasHeaderRecord = false;
                csvWriter.WriteRecords(assets);
            }

            using var readMemoryStream = new MemoryStream(writeMemoryStream.ToArray());

            try
            {
                await blob.AppendBlockAsync(readMemoryStream);
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
