using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class FileStorageService : IFileStorageService
    {
        private readonly PackageBlobNameProvider _blobNameProvider;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(
            PackageBlobNameProvider blobNameProvider,
            IBlobStorageService blobStorageService,
            ILogger<FileStorageService> logger)
        {
            _blobNameProvider = blobNameProvider;
            _blobStorageService = blobStorageService;
            _logger = logger;
        }

        public async Task StoreStreamAsync(string id, string version, FileArtifactType type, Func<Stream, Task> writeAsync)
        {
            using (var memoryStream = new MemoryStream())
            {
                await writeAsync(memoryStream);

                var blobName = _blobNameProvider.GetLatestBlobName(id, version, type);
                var contentType = GetContentType(type);
                memoryStream.Position = 0;
                await _blobStorageService.UploadStreamAsync(blobName, contentType, memoryStream);
            }
        }

        public async Task<Stream> GetStreamOrNullAsync(string id, string version, FileArtifactType type)
        {
            var blobName = _blobNameProvider.GetLatestBlobName(id, version, type);
            return await _blobStorageService.GetStreamOrNullAsync(blobName);
        }

        private static string GetContentType(FileArtifactType type)
        {
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

            return contentType;
        }
    }
}
