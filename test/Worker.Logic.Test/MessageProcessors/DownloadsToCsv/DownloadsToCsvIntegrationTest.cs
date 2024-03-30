// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public class DownloadsToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string DownloadsToCsvDir = nameof(DownloadsToCsv);
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
                serviceCollection.AddTransient(s => MockVersionSetProvider.Object);
            });
        }

        [Fact]
        public async Task DownloadsToCsv()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "downloads_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_JustV2()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure(useV1: false, useV2: true);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "downloads_08585909578854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "downloads_08585909578854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "downloads_08585908678854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_V1AndV2()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure(useV1: true, useV2: true);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "downloads_08585909578854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests.Where(r => r.Method == HttpMethod.Head && r.RequestUri.AbsolutePath.EndsWith("/downloads.v2.json", StringComparison.Ordinal)));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests.Where(r => r.Method == HttpMethod.Head && r.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json", StringComparison.Ordinal)));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests.Where(r => r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/downloads.v2.json", StringComparison.Ordinal)));
            Assert.Empty(HttpMessageHandlerFactory.SuccessRequests.Where(r => r.Method == HttpMethod.Get && r.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json", StringComparison.Ordinal)));

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "downloads_08585909578854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "downloads_08585908678854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.PackageDownloadContainerName, "latest_downloads.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.PackageDownloadContainerName, "latest_downloads.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task DownloadsToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.PackageDownloadContainerName, "latest_downloads.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.PackageDownloadContainerName, "latest_downloads.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task DownloadsToCsv_NonExistentId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step1, "latest_downloads.csv.gz");

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
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step2, "downloads_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentIdDir, Step2, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_NonExistentVersion()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string version;
            MockVersionSet.Setup(x => x.TryGetVersion("Knapcode.TorSharp", "2.0.7", out version)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetVersion("Newtonsoft.Json", "10.5.0", out version)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step1, "latest_downloads.csv.gz");

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
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step2, "downloads_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_NonExistentVersionDir, Step2, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_UncheckedIdAndVersion()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
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
            await AssertCsvBlobAsync(DownloadsToCsv_UncheckedIdAndVersionDir, Step1, "downloads_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(DownloadsToCsv_UncheckedIdAndVersionDir, Step1, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_UnicodeDuplicates()
        {
            // Arrange
            Configure(DownloadsToCsv_UnicodeDuplicatesDir);
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(DownloadsToCsv_UnicodeDuplicatesDir, Step1, "latest_downloads.csv.gz");
        }

        [Fact]
        public async Task DownloadsToCsv_JustLatest()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step1, "latest_downloads.csv.gz");
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Head && r.RequestUri.AbsoluteUri.EndsWith("/downloads.v1.json", StringComparison.Ordinal));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Get && r.RequestUri.AbsoluteUri.EndsWith("/downloads.v1.json", StringComparison.Ordinal));
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(r => r.RequestUri.AbsoluteUri.EndsWith("/downloads.v1.json", StringComparison.Ordinal)));
            HttpMessageHandlerFactory.Clear();

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(DownloadsToCsvDir, Step2, "latest_downloads.csv.gz");
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Head && r.RequestUri.AbsoluteUri.EndsWith("/downloads.v1.json", StringComparison.Ordinal));
            Assert.Single(HttpMessageHandlerFactory.SuccessRequests, r => r.Method == HttpMethod.Get && r.RequestUri.AbsoluteUri.EndsWith("/downloads.v1.json", StringComparison.Ordinal));
            Assert.Equal(2, HttpMessageHandlerFactory.SuccessRequests.Count(r => r.RequestUri.AbsoluteUri.EndsWith("/downloads.v1.json", StringComparison.Ordinal)));
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<PackageDownloads>> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure(string dirName = DownloadsToCsvDir, bool useV1 = true, bool useV2 = false)
        {
            ConfigureSettings = x =>
            {
                if (useV1)
                {
                    x.DownloadsV1Urls = new List<string> { $"http://localhost/{TestInput}/{dirName}/downloads.v1.json" };
                }

                if (useV2)
                {
                    x.DownloadsV2Urls = new List<string> { $"http://localhost/{TestInput}/{dirName}/downloads.v2.json" };
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

        protected async Task AssertCsvCountAsync(int expected)
        {
            await AssertBlobCountAsync(Options.Value.PackageDownloadContainerName, expected);
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvAsync<PackageDownloadRecord>(Options.Value.PackageDownloadContainerName, testName, stepName, "latest_downloads.csv", blobName);
        }
    }
}
