using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Entities
{
    public class PackageDownloadsToDatabaseProcessorTest
    {
        public class UpdateAsync : IDisposable
        {
            private readonly TestDirectory _testDirectory;
            private readonly List<List<PackageDownloads>> _batches;
            private readonly Mock<IPackageDownloadsClient> _client;
            private readonly Mock<IPackageService> _service;
            private readonly Mock<IETagService> _etagService;
            private readonly Mock<IBatchSizeProvider> _batchSizeProvider;
            private readonly ExplorePackagesEntitiesSettings _settings;
            private readonly Mock<IOptionsSnapshot<ExplorePackagesEntitiesSettings>> _options;
            private readonly PackageDownloadsToDatabaseProcessor _target;

            public UpdateAsync(ITestOutputHelper output)
            {
                _testDirectory = TestDirectory.Create();
                _batches = new List<List<PackageDownloads>>();

                _client = new Mock<IPackageDownloadsClient>();
                _service = new Mock<IPackageService>();
                _etagService = new Mock<IETagService>();
                _batchSizeProvider = new Mock<IBatchSizeProvider>();
                _batchSizeProvider
                    .Setup(x => x.Get(It.IsAny<BatchSizeType>()))
                    .Returns(1000);
                _settings = new ExplorePackagesEntitiesSettings
                {
                    DownloadsV1Path = Path.Combine(_testDirectory, "downloads.txt"),
                };
                _options = new Mock<IOptionsSnapshot<ExplorePackagesEntitiesSettings>>();
                _options
                    .Setup(x => x.Value)
                    .Returns(() => _settings);

                _service
                    .Setup(x => x.AddOrUpdatePackagesAsync(It.IsAny<IEnumerable<PackageDownloads>>()))
                    .Returns(Task.CompletedTask)
                    .Callback<IEnumerable<PackageDownloads>>(b => _batches.Add(b.ToList()));

                _target = new PackageDownloadsToDatabaseProcessor(
                    _client.Object,
                    _service.Object,
                    _etagService.Object,
                    _batchSizeProvider.Object,
                    _options.Object,
                    output.GetLogger< PackageDownloadsToDatabaseProcessor>());
            }

            public void Dispose()
            {
                _testDirectory?.Dispose();
            }

            [Fact]
            public async Task EmitsOlderDownloadRecordIfNotPresentInNewFile()
            {
                // Arrange
                File.WriteAllLines(_settings.DownloadsV1Path, new[] { "[\"A\",\"1.0.0\",10]", "[\"B\",\"2.0.0\",20]", "[\"C\",\"3.0.0\",30]" });

                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(new[]
                        {
                            new PackageDownloads("C", "3.0.0", 30), // No change.
                            new PackageDownloads("A", "1.0.0", 10), // No change.
                        }.ToList().GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                var batch = Assert.Single(_batches);
                var downloads = Assert.Single(batch);
                Assert.Equal(new PackageDownloads("B", "2.0.0", 20), downloads);
            }

            [Fact]
            public async Task EmitsNewerDownloadRecordIfNotPresentInOldFile()
            {
                // Arrange
                File.WriteAllLines(_settings.DownloadsV1Path, new[] { "[\"A\",\"1.0.0\",10]", "[\"C\",\"3.0.0\",30]" });

                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(new[]
                        {
                            new PackageDownloads("B", "2.0.0", 20), // New.
                            new PackageDownloads("C", "3.0.0", 30), // No change.
                            new PackageDownloads("A", "1.0.0", 10), // No change.
                        }.ToList().GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                var batch = Assert.Single(_batches);
                var downloads = Assert.Single(batch);
                Assert.Equal(new PackageDownloads("B", "2.0.0", 20), downloads);
            }

            [Fact]
            public async Task EmitsNewerDownloadRecordIfDownloadsIncreased()
            {
                // Arrange
                File.WriteAllLines(_settings.DownloadsV1Path, new[] { "[\"A\",\"1.0.0\",10]", "[\"B\",\"2.0.0\",20]" });

                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(new[]
                        {
                            new PackageDownloads("B", "2.0.0", 25), // More downloads.
                            new PackageDownloads("A", "1.0.0", 10), // No change.
                        }.ToList().GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                var batch = Assert.Single(_batches);
                var downloads = Assert.Single(batch);
                Assert.Equal(new PackageDownloads("B", "2.0.0", 25), downloads);
            }

            [Fact]
            public async Task EmitsNewerDownloadRecordIfDownloadsDecreased()
            {
                // Arrange
                File.WriteAllLines(_settings.DownloadsV1Path, new[] { "[\"A\",\"1.0.0\",10]", "[\"B\",\"2.0.0\",20]" });

                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(new[]
                        {
                            new PackageDownloads("B", "2.0.0", 15), // Fewer downloads.
                            new PackageDownloads("A", "1.0.0", 10), // No change.
                        }.ToList().GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                var batch = Assert.Single(_batches);
                var downloads = Assert.Single(batch);
                Assert.Equal(new PackageDownloads("B", "2.0.0", 15), downloads);
            }

            [Fact]
            public async Task EmitsNothingIfNothingChanged()
            {
                // Arrange
                File.WriteAllLines(_settings.DownloadsV1Path, new[] { "[\"A\",\"1.0.0\",10]", "[\"B\",\"2.0.0\",20]" });

                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(new[]
                        {
                            new PackageDownloads("B", "2.0.0", 20), // No change.
                            new PackageDownloads("A", "1.0.0", 10), // No change.
                        }.ToList().GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                Assert.Empty(_batches);
            }

            [Fact]
            public async Task EmitsAllNewRecordsIfExistingFileIsMissing()
            {
                // Arrange
                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(new[]
                        {
                            new PackageDownloads("B", "2.0.0", 20), // No change.
                            new PackageDownloads("A", "1.0.0", 10), // No change.
                        }.ToList().GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                var batch = Assert.Single(_batches);
                Assert.Equal(2, batch.Count);
                Assert.Equal(new PackageDownloads("A", "1.0.0", 10), batch[0]);
                Assert.Equal(new PackageDownloads("B", "2.0.0", 20), batch[1]);
            }

            [Fact]
            public async Task EmitsAllOldRecordsIfNewFileIsEmpty()
            {
                // Arrange
                File.WriteAllLines(_settings.DownloadsV1Path, new[] { "[\"A\",\"1.0.0\",10]", "[\"B\",\"2.0.0\",20]" });

                _client
                    .Setup(x => x.GetPackageDownloadSetAsync(It.IsAny<string>()))
                    .ReturnsAsync(new PackageDownloadSet(
                        "\"foo\"",
                        new MemoryAsyncEnumerator<PackageDownloads>(Enumerable
                            .Empty<PackageDownloads>()
                            .GetEnumerator())));

                // Act
                await _target.UpdateAsync();

                // Assert
                var batch = Assert.Single(_batches);
                Assert.Equal(2, batch.Count);
                Assert.Equal(new PackageDownloads("A", "1.0.0", 10), batch[0]);
                Assert.Equal(new PackageDownloads("B", "2.0.0", 20), batch[1]);
            }
        }

        private class MemoryAsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> _enumerator;

            public MemoryAsyncEnumerator(IEnumerator<T> enumerator)
            {
                _enumerator = enumerator;
            }

            public T Current => _enumerator.Current;

            public ValueTask DisposeAsync()
            {
                _enumerator.Dispose();
                return default;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                return new ValueTask<bool>(_enumerator.MoveNext());
            }
        }
    }
}
