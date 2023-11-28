// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;

namespace NuGet.Insights.Worker.VerifiedPackagesToCsv
{
    public class VerifiedPackagesToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string VerifiedPackagesToCsvDir = nameof(VerifiedPackagesToCsv);
        private const string VerifiedPackagesToCsv_NonExistentIdDir = nameof(VerifiedPackagesToCsv_NonExistentId);
        private const string VerifiedPackagesToCsv_UncheckedIdDir = nameof(VerifiedPackagesToCsv_UncheckedId);

        [Fact]
        public async Task VerifiedPackagesToCsv()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "verified_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(3);
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "verified_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step2, "verified_packages_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step2, "latest_verified_packages.csv.gz");
        }

        [Fact]
        public async Task VerifiedPackagesToCsv_NoOp()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task VerifiedPackagesToCsv_DifferentVersionSet()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
            var blobA = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
            var propertiesA = await blobA.GetPropertiesAsync();

            // Arrange
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
            var blobB = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
            var propertiesB = await blobB.GetPropertiesAsync();
            Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
        }

        [Fact]
        public async Task VerifiedPackagesToCsv_NonExistentId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            string id;
            MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
            MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step1, "verified_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step1, "latest_verified_packages.csv.gz");

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
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step1, "verified_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step2, "verified_packages_08585908696854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step2, "latest_verified_packages.csv.gz");
        }

        [Fact]
        public async Task VerifiedPackagesToCsv_UncheckedId()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;

            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());
            MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_UncheckedIdDir, Step1, "verified_packages_08585909596854775807.csv.gz");
            await AssertCsvBlobAsync(VerifiedPackagesToCsv_UncheckedIdDir, Step1, "latest_verified_packages.csv.gz");
        }

        [Fact]
        public async Task VerifiedPackagesToCsv_JustLatest()
        {
            // Arrange
            Configure();
            var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
            await service.InitializeAsync();
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");

            // Arrange
            SetData(Step2);
            Assert.True(await service.StartAsync());

            // Act
            await ProcessQueueAsync(service);

            // Assert
            await AssertCsvCountAsync(1);
            await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step2, "latest_verified_packages.csv.gz");
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void Configure()
        {
            ConfigureSettings = x => x.VerifiedPackagesV1Urls = new List<string> { $"http://localhost/{TestInput}/{VerifiedPackagesToCsvDir}/verifiedPackages.json" };
            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/verifiedPackages.json", StringComparison.Ordinal))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestInput}/{VerifiedPackagesToCsvDir}/{stepName}/verifiedPackages.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        protected async Task AssertCsvCountAsync(int expected)
        {
            await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, expected);
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvAsync<VerifiedPackageRecord>(Options.Value.VerifiedPackageContainerName, testName, stepName, "latest_verified_packages.csv", blobName);
        }

        public VerifiedPackagesToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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
