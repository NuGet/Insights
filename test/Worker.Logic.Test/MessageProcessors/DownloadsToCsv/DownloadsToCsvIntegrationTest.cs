// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public class DownloadsToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        private const string DownloadsToCsvDir = nameof(DownloadsToCsv);
        private const string DownloadsToCsv_JustV2Dir = nameof(DownloadsToCsv_JustV2);
        private const string DownloadsToCsv_NonExistentVersionDir = nameof(DownloadsToCsv_NonExistentVersion);
        private const string DownloadsToCsv_NonExistentIdDir = nameof(DownloadsToCsv_NonExistentId);
        private const string DownloadsToCsv_UnicodeDuplicatesDir = nameof(DownloadsToCsv_UnicodeDuplicates);
        private const string DownloadsToCsv_UncheckedIdAndVersionDir = nameof(DownloadsToCsv_UncheckedIdAndVersion);

        public DownloadsToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            SetupDefaultMockVersionSet();
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddSingleton(s => MockVersionSetProvider.Object);
            });
        }

        [Fact]
        public async Task DownloadsToCsv()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2);
        }

        [Fact]
        public async Task DownloadsToCsv_BigMode()
        {
            // Arrange
            Configure(useBigMode: true);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2);
        }

        [Fact]
        public async Task DownloadsToCsv_JustV2()
        {
            // Arrange
            Configure(useV1: false, useV2: true);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_JustV2Dir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_JustV2Dir, Step2);
        }

        [Fact]
        public async Task DownloadsToCsv_V1AndV2()
        {
            // Arrange
            Configure(useV1: true, useV2: true);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_JustV2Dir, Step1);
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Head && r.RequestUri.AbsolutePath.EndsWith("/downloads.v2.json", StringComparison.Ordinal));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Head && r.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json", StringComparison.Ordinal));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/downloads.v2.json", StringComparison.Ordinal));
            Assert.DoesNotContain(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json", StringComparison.Ordinal));

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_JustV2Dir, Step2);
        }

        [Fact]
        public async Task DownloadsToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1);
            var blobA = await GetBlobAsync(Options.Value.PackageDownloadContainerName, 0);
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1);
            var blobB = await GetBlobAsync(Options.Value.PackageDownloadContainerName, 0);
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task DownloadsToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1);
            var blobA = await GetBlobAsync(Options.Value.PackageDownloadContainerName, 0);
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1);
            var blobB = await GetBlobAsync(Options.Value.PackageDownloadContainerName, 0);
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task DownloadsToCsv_NonExistentId()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());
            MockVersionSet
                .Setup(x => x.TryGetId("Knapcode.TorSharp", out id))
                .Returns(true)
                .Callback(new TryGetId((string id, out string outId) => outId = "knapcode.TORSHARP"));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step2);
        }

        [Fact]
        public async Task DownloadsToCsv_NonExistentVersion()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string version;
            MockVersionSet.Setup(x => x.TryGetVersion("Knapcode.TorSharp", "2.0.7", out version)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetVersion("Newtonsoft.Json", "10.5.0", out version)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step1);

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());
            MockVersionSet
                .Setup(x => x.TryGetVersion("Knapcode.TorSharp", "2.0.7", out version))
                .Returns(true)
                .Callback(new TryGetVersion((string id, string version, out string outVersion) => outVersion = version));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step2);
        }

        [Fact]
        public async Task DownloadsToCsv_UncheckedIdAndVersion()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string version;
            MockVersionSet.Setup(x => x.TryGetVersion("Knapcode.TorSharp", "2.0.7", out version)).Returns(false);
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA", "Knapcode.TorSharp" });
            MockVersionSet.Setup(x => x.GetUncheckedVersions("UncheckedA")).Returns(new[] { "2.0.0", "1.0.0" });
            MockVersionSet.Setup(x => x.GetUncheckedVersions("UncheckedB")).Returns(new[] { "3.0.0" });
            MockVersionSet.Setup(x => x.GetUncheckedVersions("Knapcode.TorSharp")).Returns(new[] { "0.0.1" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_UncheckedIdAndVersionDir, Step1);
        }

        [Fact]
        public async Task DownloadsToCsv_UnicodeDuplicates()
        {
            // Arrange
            Configure(DownloadsToCsv_UnicodeDuplicatesDir);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<PackageDownloadRecord>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_UnicodeDuplicatesDir, Step1);
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<PackageDownloadRecord> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure(string dirName = DownloadsToCsvDir, bool useV1 = true, bool useV2 = false, bool useBigMode = false)
        {
            ConfigureSettings = x =>
            {
                x.UseBlobClientForExternalData = false;

                if (useV1)
                {
                    x.DownloadsV1Urls = new List<string> { $"http://localhost/{TestInput}/{dirName}/downloads.v1.json" };
                }

                if (useV2)
                {
                    x.DownloadsV2Urls = new List<string> { $"http://localhost/{TestInput}/{dirName}/downloads.v2.json" };
                }
            };

            ConfigureWorkerSettings = x =>
            {
                if (useBigMode)
                {
                    x.AppendResultBigModeRecordThreshold = 0;
                }
            };

            SetData(Step1, dirName);
        }

        private void SetData(string stepName, string dirName = DownloadsToCsvDir)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsoluteUri == Options.Value.DownloadsV1Urls.SingleOrDefault())
                {
                    var newReq = Clone(req);
                    newReq.Headers.TryAddWithoutValidation("Original", req.RequestUri.AbsoluteUri);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{dirName}/{stepName}/downloads.v1.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                if (req.RequestUri.AbsoluteUri == Options.Value.DownloadsV2Urls.SingleOrDefault())
                {
                    var newReq = Clone(req);
                    newReq.Headers.TryAddWithoutValidation("Original", req.RequestUri.AbsoluteUri);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{dirName}/{stepName}/downloads.v2.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        private Task AssertCsvBlobAsync(string testName, string stepName)
        {
            return AssertCsvAsync<PackageDownloadRecord>(Options.Value.PackageDownloadContainerName, testName, stepName, 0);
        }
    }
}
