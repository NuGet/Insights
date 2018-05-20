using System;
using System.IO;
using System.Net;
using System.Threading;
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
                _logger.LogInformation("  {Method} {BlobUri}", "GET", blob.Uri);
                return await blob.OpenReadAsync(
                    accessCondition: null,
                    options: null,
                    operationContext: GetLoggingOperationContext());
            }
            catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private OperationContext GetLoggingOperationContext()
        {
            var operationContext = new OperationContext();

            var receivedCount = 0;
            operationContext.ResponseReceived += (object sender, RequestEventArgs e) =>
            {
                // For some reason this event handler is being used again... keep getting HTTP 206 responses.
                if (Interlocked.Increment(ref receivedCount) != 1)
                {
                    return;
                }

                _logger.LogInformation("  {StatusCode} {RequestUri}", (HttpStatusCode)e.RequestInformation.HttpStatusCode, e.RequestUri);
            };

            return operationContext;
        }

        public virtual async Task UploadStreamAsync(string blobName, string contentType, Stream stream)
        {
            var blob = GetBlob(blobName);
            blob.Properties.ContentType = contentType;
            _logger.LogInformation("  {Method} {BlobUri}", "PUT", blob.Uri);
            await blob.UploadFromStreamAsync(
                stream,
                accessCondition: null,
                options: null,
                operationContext: GetLoggingOperationContext());
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return Container.GetBlockBlobReference(blobName);
        }
    }
}
