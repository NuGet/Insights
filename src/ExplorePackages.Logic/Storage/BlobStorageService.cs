using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Logic
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly Lazy<CloudBlobContainer> _lazyContainer;

        public BlobStorageService(ExplorePackagesSettings settings, ILogger<BlobStorageService> logger)
        {
            _settings = settings;
            _logger = logger;
            _lazyContainer = new Lazy<CloudBlobContainer>(() =>
            {
                var account = CloudStorageAccount.Parse(_settings.StorageConnectionString);
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(_settings.StorageContainerName);

                return container;
            });
        }

        private CloudBlobContainer Container => _lazyContainer.Value;

        public virtual bool IsEnabled => _settings.StorageConnectionString != null && _settings.StorageContainerName != null;

        public virtual async Task<Stream> GetStreamOrNullAsync(string blobName)
        {
            var blob = GetBlob(blobName);

            try
            {
                return await blob.OpenReadAsync();
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public virtual async Task UploadStreamAsync(string blobName, string contentType, Stream stream)
        {
            var blob = GetBlob(blobName);
            blob.Properties.ContentType = contentType;
            _logger.LogInformation("  PUT {BlobUri}", blob.Uri);
            await blob.UploadFromStreamAsync(stream);
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return Container.GetBlockBlobReference(blobName);
        }
    }
}
