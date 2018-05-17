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

        public async Task StoreMZipStreamAsync(string id, string version, Func<Stream, Task> writeAsync)
        {
            var filePath = _filePathProvider.GetLatestMZipFilePath(id, version);
            await SafeFileWriter.WriteAsync(filePath, writeAsync, _logger);
            await CopyMZipFileToBlobAsync(id, version, filePath, required: false);
        }

        public async Task CopyMZipFileToBlobIfExistsAsync(string id, string version)
        {
            var filePath = _filePathProvider.GetLatestMZipFilePath(id, version);
            if (!File.Exists(filePath))
            {
                return;
            }

            await CopyMZipFileToBlobAsync(id, version, filePath, required: true);
        }

        public Task<Stream> GetMZipStreamOrNullAsync(string id, string version)
        {
            var filePath = _filePathProvider.GetLatestMZipFilePath(id, version);
            return Task.FromResult(GetStreamOrNull(filePath));
        }

        public Task DeleteMZipStreamAsync(string id, string version)
        {
            var filePath = _filePathProvider.GetLatestMZipFilePath(id, version);
            DeleteFile(filePath);
            return Task.CompletedTask;
        }

        public async Task StoreNuspecStreamAsync(string id, string version, Func<Stream, Task> writeAsync)
        {
            var filePath = _filePathProvider.GetLatestNuspecFilePath(id, version);
            await SafeFileWriter.WriteAsync(filePath, writeAsync, _logger);
            await CopyNuspecFileToBlobAsync(id, version, filePath, required: false);
        }

        public async Task CopyNuspecFileToBlobIfExistsAsync(string id, string version)
        {
            var filePath = _filePathProvider.GetLatestNuspecFilePath(id, version);
            if (!File.Exists(filePath))
            {
                return;
            }

            await CopyNuspecFileToBlobAsync(id, version, filePath, required: true);
        }

        public Task<Stream> GetNuspecStreamOrNullAsync(string id, string version)
        {
            var filePath = _filePathProvider.GetLatestNuspecFilePath(id, version);
            return Task.FromResult(GetStreamOrNull(filePath));
        }

        public Task DeleteNuspecStreamAsync(string id, string version)
        {
            var filePath = _filePathProvider.GetLatestNuspecFilePath(id, version);
            DeleteFile(filePath);
            return Task.CompletedTask;
        }

        private async Task CopyMZipFileToBlobAsync(string id, string version, string filePath, bool required)
        {
            var blobName = _blobNameProvider.GetLatestMZipBlobName(id, version);
            await CopyFileToBlobAsync(filePath, blobName, "application/octet-stream", required);
        }

        private async Task CopyNuspecFileToBlobAsync(string id, string version, string filePath, bool required)
        {
            var blobName = _blobNameProvider.GetLatestNuspecPath(id, version);
            await CopyFileToBlobAsync(filePath, blobName, "application/xml", required);
        }

        private async Task CopyFileToBlobAsync(
            string filePath,
            string blobName,
            string contentType,
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

            var blob = BlobContainer.GetBlockBlobReference(blobName);
            _logger.LogInformation("  PUT {BlobUri}", blob.Uri);
            blob.Properties.ContentType = contentType;
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

        private static void DeleteFile(string filePath)
        {
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
