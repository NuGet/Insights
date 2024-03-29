// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.ExcludedPackagesToCsv
{
    public class ExcludedPackagesToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string ExcludedPackagesToCsvDir = nameof(ExcludedPackagesToCsv);
        private const string ExcludedPackagesToCsv_NonExistentIdDir = nameof(ExcludedPackagesToCsv_NonExistentId);
        private const string ExcludedPackagesToCsv_UncheckedIdDir = nameof(ExcludedPackagesToCsv_UncheckedId);

        [Fact]
        public async Task ExcludedPackagesToCsv()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "excluded_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "latest_excluded_packages.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "excluded_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step2, "excluded_packages_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step2, "latest_excluded_packages.csv.gz");
        }

        [Fact]
        public async Task ExcludedPackagesToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "latest_excluded_packages.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.ExcludedPackageContainerName, "latest_excluded_packages.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "latest_excluded_packages.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.ExcludedPackageContainerName, "latest_excluded_packages.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task ExcludedPackagesToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "latest_excluded_packages.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.ExcludedPackageContainerName, "latest_excluded_packages.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "latest_excluded_packages.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.ExcludedPackageContainerName, "latest_excluded_packages.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task ExcludedPackagesToCsv_NonExistentId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_NonExistentIdDir, Step1, "excluded_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_NonExistentIdDir, Step1, "latest_excluded_packages.csv.gz");

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
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_NonExistentIdDir, Step1, "excluded_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_NonExistentIdDir, Step2, "excluded_packages_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_NonExistentIdDir, Step2, "latest_excluded_packages.csv.gz");
        }

        [Fact]
        public async Task ExcludedPackagesToCsv_UncheckedId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_UncheckedIdDir, Step1, "excluded_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(ExcludedPackagesToCsv_UncheckedIdDir, Step1, "latest_excluded_packages.csv.gz");
        }

        [Fact]
        public async Task ExcludedPackagesToCsv_JustLatest()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step1, "latest_excluded_packages.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(ExcludedPackagesToCsvDir, Step2, "latest_excluded_packages.csv.gz");
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<ExcludedPackage>> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure()
        {
            ConfigureSettings = x => x.ExcludedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/{ExcludedPackagesToCsvDir}/excludedPackages.json" };
            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/excludedPackages.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{ExcludedPackagesToCsvDir}/{stepName}/excludedPackages.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        protected async Task AssertCsvCountAsync(int expected)
        {
            await AssertBlobCountAsync(Options.Value.ExcludedPackageContainerName, expected);
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvAsync<ExcludedPackageRecord>(Options.Value.ExcludedPackageContainerName, testName, stepName, "latest_excluded_packages.csv", blobName);
        }

        public ExcludedPackagesToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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
    }
}
