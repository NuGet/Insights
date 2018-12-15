using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Logic
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly Lazy<CloudBlobContainer> _lazyContainer;

        public BlobStorageService(
            HttpClient httpClient,
            ExplorePackagesSettings settings,
            ILogger<BlobStorageService> logger)
        {
            _httpClient = httpClient;
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

        public virtual async Task<bool> TryDownloadStreamAsync(string blobName, Stream destinationStream)
        {
            var blob = GetBlob(blobName);

            if (_settings.IsStorageContainerPublic)
            {
                using (var response = await _httpClient.GetAsync(blob.Uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return false;
                    }

                    response.EnsureSuccessStatusCode();

                    using (var responseStream = await response.Content.ReadAsStreamAsync())
                    {
                        await responseStream.CopyToAsync(destinationStream);
                    }

                    return true;
                }
            }
            else
            {
                try
                {
                    _logger.LogDebug("  {Method} {BlobUri}", "GET", blob.Uri);
                    using (var responseStream = await blob.OpenReadAsync(
                        accessCondition: null,
                        options: null,
                        operationContext: GetLoggingOperationContext()))
                    {
                        await responseStream.CopyToAsync(destinationStream);
                    }

                    return true;
                }
                catch (StorageException ex) when (ex.RequestInformation?.HttpStatusCode == (int)HttpStatusCode.NotFound)
                {
                    return false;
                }
            }
        }

        private OperationContext GetLoggingOperationContext()
        {
            var operationContext = new OperationContext();

            var receivedCount = 0;
            var stopwatch = Stopwatch.StartNew();
            operationContext.ResponseReceived += (object sender, RequestEventArgs e) =>
            {
                // For some reason this event handler is being used again... keep getting HTTP 206 responses.
                if (Interlocked.Increment(ref receivedCount) != 1)
                {
                    return;
                }

                var duration = e.RequestInformation.EndTime - e.RequestInformation.StartTime;
                _logger.LogDebug(
                    "  {StatusCode} {RequestUri} {Milliseconds}ms",
                    (HttpStatusCode)e.RequestInformation.HttpStatusCode,
                    e.RequestUri,
                    stopwatch.ElapsedMilliseconds);
            };

            return operationContext;
        }

        public virtual async Task UploadStreamAsync(string blobName, string contentType, Stream stream)
        {
            var blob = GetBlob(blobName);
            blob.Properties.ContentType = contentType;
            _logger.LogDebug("  {Method} {BlobUri}", "PUT", blob.Uri);
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
