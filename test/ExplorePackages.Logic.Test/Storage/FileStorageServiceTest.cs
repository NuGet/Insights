using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Logic
{
    public class FileStorageServiceTest : IAsyncLifetime
    {
        private const string Id = "Newtonsoft.Json";
        private const string Version = "9.0.1";

        private readonly TestDirectory _directory;
        private readonly ITestOutputHelper _output;
        private readonly ExplorePackagesSettings _settings;
        private readonly PackageBlobNameProvider _blobNameProvider;
        private readonly Mock<BlobStorageService> _blobStorageService;
        private readonly MemoryCache _memoryCache;
        private readonly FileStorageService _target;

        public FileStorageServiceTest(ITestOutputHelper output)
        {
            _directory = TestDirectory.Create();

            _output = output;
            _settings = new ExplorePackagesSettings
            {
                PackagePath = _directory,
                StorageConnectionString = "UseDevelopmentStorage=true",
                StorageContainerName = Guid.NewGuid().ToString("N"),
            };
            _blobNameProvider = new PackageBlobNameProvider();
            _blobStorageService = new Mock<BlobStorageService>(_settings, _output.GetLogger<BlobStorageService>())
            {
                CallBase = true,
            };
            _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));

            _target = new FileStorageService(
                _blobNameProvider,
                _blobStorageService.Object,
                _memoryCache,
                _output.GetLogger<FileStorageService>());
        }

        [Theory]
        [InlineData(FileArtifactType.Nuspec)]
        [InlineData(FileArtifactType.MZip)]
        public async Task ReturnsNullWithNonExistentFile(FileArtifactType type)
        {
            // Arrange & Act
            using (var actual = await _target.GetStreamOrNullAsync(Id, Version, type))
            {
                // Assert
                Assert.Null(actual);

                _blobStorageService.Verify(
                    x => x.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()),
                    Times.Never);
                _blobStorageService.Verify(
                    x => x.GetStreamOrNullAsync(It.IsAny<string>()),
                    Times.Once);
            }
        }

        [Theory]
        [InlineData(FileArtifactType.Nuspec)]
        [InlineData(FileArtifactType.MZip)]
        public async Task CanReplaceExistingFile(FileArtifactType type)
        {
            // Arrange
            var initial = new MemoryStream(Encoding.UTF8.GetBytes("Initial!"));
            var expected = new MemoryStream(Encoding.UTF8.GetBytes("Hello, world!"));

            await _target.StoreStreamAsync(
                Id,
                Version,
                type,
                dest => initial.CopyToAsync(dest));

            _blobStorageService.ResetCalls();

            // Act
            await _target.StoreStreamAsync(
                Id,
                Version,
                type,
                dest => expected.CopyToAsync(dest));

            // Assert
            _blobStorageService.Verify(
                x => x.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()),
                Times.Once);
            _blobStorageService.Verify(
                x => x.GetStreamOrNullAsync(It.IsAny<string>()),
                Times.Never);

            _blobStorageService.ResetCalls();

            using (var actual = await _target.GetStreamOrNullAsync(Id, Version, type))
            {
                AssertSameStreams(expected, actual);

                _blobStorageService.Verify(
                    x => x.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()),
                    Times.Never);
                _blobStorageService.Verify(
                    x => x.GetStreamOrNullAsync(It.IsAny<string>()),
                    Times.Never);
            }
        }

        [Theory]
        [InlineData(FileArtifactType.Nuspec)]
        [InlineData(FileArtifactType.MZip)]
        public async Task CanWriteAndReadAFile(FileArtifactType type)
        {
            // Arrange
            var expected = new MemoryStream(Encoding.UTF8.GetBytes("Hello, world!"));

            // Act & Assert
            await _target.StoreStreamAsync(
                Id,
                Version,
                type,
                dest => expected.CopyToAsync(dest));

            _blobStorageService.Verify(
                x => x.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()),
                Times.Once);
            _blobStorageService.Verify(
                x => x.GetStreamOrNullAsync(It.IsAny<string>()),
                Times.Never);

            _blobStorageService.ResetCalls();

            using (var actual = await _target.GetStreamOrNullAsync(Id, Version, type))
            {
                AssertSameStreams(expected, actual);

                _blobStorageService.Verify(
                    x => x.UploadStreamAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>()),
                    Times.Never);
                _blobStorageService.Verify(
                    x => x.GetStreamOrNullAsync(It.IsAny<string>()),
                    Times.Never);
            }
        }

        public async Task DisposeAsync()
        {
            _directory.Dispose();
            await GetContainer().DeleteIfExistsAsync();
        }

        public async Task InitializeAsync()
        {
            await GetContainer().CreateIfNotExistsAsync();
        }

        private CloudBlobContainer GetContainer()
        {
            return CloudStorageAccount
                .Parse(_settings.StorageConnectionString)
                .CreateCloudBlobClient()
                .GetContainerReference(_settings.StorageContainerName);
        }

        private void AssertSameStreams(Stream expected, Stream actual)
        {
            var expectedBytes = GetStreamBytes(expected);
            var actualBytes = GetStreamBytes(actual);
            Assert.Equal(expectedBytes, actualBytes);
        }

        private byte[] GetStreamBytes(Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.Position = 0;
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}
