using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using NuGet.Protocol;

namespace Knapcode.ExplorePackages.Logic
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly HttpSource _httpSource;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _options;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly Lazy<CloudBlobContainer> _lazyContainer;

        public BlobStorageService(
            HttpSource httpSource,
            IOptionsSnapshot<ExplorePackagesSettings> options,
            ILogger<BlobStorageService> logger)
        {
            _httpSource = httpSource;
            _options = options;
            _logger = logger;
            _lazyContainer = new Lazy<CloudBlobContainer>(() =>
            {
                var account = CloudStorageAccount.Parse(_options.Value.StorageConnectionString);
                var client = account.CreateCloudBlobClient();
                var container = client.GetContainerReference(_options.Value.StorageContainerName);

                return container;
            });
        }

        private CloudBlobContainer Container => _lazyContainer.Value;

        public virtual async Task<bool> TryDownloadStreamAsync(string blobName, Stream destinationStream)
        {
            var blob = GetBlob(blobName);

            if (_options.Value.IsStorageContainerPublic)
            {
                var nuGetLogger = _logger.ToNuGetLogger();
                return await _httpSource.ProcessStreamAsync(
                    new HttpSourceRequest(blob.Uri, nuGetLogger)
                    {
                        IgnoreNotFounds = true,
                    },
                    async responseStream =>
                    {
                        if (responseStream == null)
                        {
                            return false;
                        }

                        await responseStream.CopyToAsync(destinationStream);
                        return true;
                    },
                    nuGetLogger,
                    CancellationToken.None);
            }
            else
            {
                try
                {
                    LogRequest(_logger, HttpMethod.Get, blob);
                    using (var responseStream = await blob.OpenReadAsync(
                        accessCondition: null,
                        options: null,
                        operationContext: GetLoggingOperationContext(_logger)))
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

        public virtual async Task UploadStreamAsync(
            string blobName,
            string contentType,
            Stream stream,
            AccessCondition accessCondition)
        {
            var blob = GetBlob(blobName);
            blob.Properties.ContentType = contentType;
            LogRequest(_logger, HttpMethod.Put, blob);
            await blob.UploadFromStreamAsync(
                stream,
                accessCondition: accessCondition,
                options: null,
                operationContext: GetLoggingOperationContext(_logger));
        }

        private CloudBlockBlob GetBlob(string blobName)
        {
            return Container.GetBlockBlobReference(blobName);
        }

        public static void LogRequest(ILogger logger, HttpMethod method, CloudBlockBlob blob)
        {
            logger.LogDebug("  {Method} {BlobUri}", method, blob.Uri);
        }

        public static OperationContext GetLoggingOperationContext(ILogger logger)
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

                // Remove the query string from the logged URI. It could contain a SAS token.
                var requestUriBuilder = new UriBuilder(e.RequestUri);
                requestUriBuilder.Query = null;
                var sanitizedRequestUri = requestUriBuilder.Uri.AbsoluteUri;

                var duration = e.RequestInformation.EndTime - e.RequestInformation.StartTime;
                logger.LogDebug(
                    "  {StatusCode} {RequestUri} {Milliseconds}ms",
                    (HttpStatusCode)e.RequestInformation.HttpStatusCode,
                    sanitizedRequestUri,
                    stopwatch.ElapsedMilliseconds);
            };

            return operationContext;
        }

        internal static void LogRequest(ILogger<BlobStorageService> logger, object httpMe)
        {
            throw new NotImplementedException();
        }
    }
}
