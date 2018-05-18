using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ExplorePackages.Logic
{
    public class FileStorageService : IFileStorageService
    {
        private readonly PackageFilePathProvider _filePathProvider;
        private readonly PackageBlobNameProvider _blobNameProvider;
        private readonly ExplorePackagesSettings _settings;
        private readonly ILogger<FileStorageService> _logger;

        private readonly Lazy<CloudBlobContainer> _lazyBlobContainer;

        public FileStorageService(
            PackageFilePathProvider filePathProvider,
            PackageBlobNameProvider blobNameProvider,
            ExplorePackagesSettings settings,
            ILogger<FileStorageService> logger)
        {
            _filePathProvider = filePathProvider;
            _blobNameProvider = blobNameProvider;
            _settings = settings;
            _logger = logger;

            _lazyBlobContainer = new Lazy<CloudBlobContainer>(GetBlobContainer);
        }

        private CloudBlobContainer BlobContainer => _lazyBlobContainer.Value;

        public async Task StoreStreamAsync(string id, string version, FileArtifactType type, Func<Stream, Task> writeAsync)
        {
            var filePath = _filePathProvider.GetLatestFilePath(id, version, type);

            await SafeFileWriter.WriteAsync(filePath, writeAsync, _logger);

            await CopyFileToBlobAsync(id, version, type, filePath, required: false);
        }

        public async Task CopyFileToBlobIfExistsAsync(string id, string version, FileArtifactType type)
        {
            var filePath = _filePathProvider.GetLatestFilePath(id, version, type);
            if (!File.Exists(filePath))
            {
                return;
            }

            await CopyFileToBlobAsync(id, version, type, filePath, required: true);
        }

        public Task<Stream> GetStreamOrNullAsync(string id, string version, FileArtifactType type)
        {
            var filePath = _filePathProvider.GetLatestFilePath(id, version, type);

            var stream = GetStreamOrNull(filePath);

            return Task.FromResult(stream);
        }

        public Task DeleteStreamAsync(string id, string version, FileArtifactType type)
        {
            var filePath = _filePathProvider.GetLatestFilePath(id, version, type);

            try
            {
                File.Delete(filePath);
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }

            return Task.CompletedTask;
        }

        private async Task CopyFileToBlobAsync(
            string id,
            string version,
            FileArtifactType type,
            string filePath,
            bool required)
        {
            if (BlobContainer == null)
            {
                if (!required)
                {
                    return;
                }
                else
                {
                    throw new InvalidOperationException("No Azure Blob Storage connection details are configured.");
                }
            }

            var blobName = _blobNameProvider.GetLatestBlobName(id, version, type);
            var blob = BlobContainer.GetBlockBlobReference(blobName);

            string contentType;
            switch (type)
            {
                case FileArtifactType.Nuspec:
                    contentType = "application/xml";
                    break;
                case FileArtifactType.MZip:
                    contentType = "application/octet-stream";
                    break;
                default:
                    throw new NotSupportedException($"The file artifact type {type} is not supported.");
            }
            blob.Properties.ContentType = contentType;

            _logger.LogInformation("  PUT {BlobUri}", blob.Uri);
            await blob.UploadFromFileAsync(filePath);
        }

        private CloudBlobContainer GetBlobContainer()
        {
            if (_settings.StorageConnectionString == null || _settings.StorageContainerName == null)
            {
                return null;
            }

            var account = CloudStorageAccount.Parse(_settings.StorageConnectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(_settings.StorageContainerName);

            return container;
        }

        private static Stream GetStreamOrNull(string filePath)
        {
            Stream stream = null;
            try
            {
                return new FileStream(filePath, FileMode.Open);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch
            {
                stream?.Dispose();
                throw;
            }
        }
    }
}
