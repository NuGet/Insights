// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.VerifiedPackagesToCsv
{
    public class VerifiedPackagesToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string VerifiedPackagesToCsvDir = nameof(VerifiedPackagesToCsv);
        private const string VerifiedPackagesToCsv_NonExistentIdDir = nameof(VerifiedPackagesToCsv_NonExistentId);
        private const string VerifiedPackagesToCsv_UncheckedIdDir = nameof(VerifiedPackagesToCsv_UncheckedId);

        public VerifiedPackagesToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            MockVersionSet.SetReturnsDefault(true);
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient(s => MockVersionSetProvider.Object);
            });
        }

        public class VerifiedPackagesToCsv : VerifiedPackagesToCsvIntegrationTest
        {
            public VerifiedPackagesToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "verified_packages_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 3);
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "verified_packages_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step2, "verified_packages_08585908696854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step2, "latest_verified_packages.csv.gz");
            }
        }

        public class VerifiedPackagesToCsv_NoOp : VerifiedPackagesToCsvIntegrationTest
        {
            public VerifiedPackagesToCsv_NoOp(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
                var blobA = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
                var propertiesA = await blobA.GetPropertiesAsync();

                // Arrange
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 1);
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
                var blobB = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
                var propertiesB = await blobB.GetPropertiesAsync();
                Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
            }
        }

        public class VerifiedPackagesToCsv_DifferentVersionSet : VerifiedPackagesToCsvIntegrationTest
        {
            public VerifiedPackagesToCsv_DifferentVersionSet(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
                var blobA = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
                var propertiesA = await blobA.GetPropertiesAsync();

                // Arrange
                await service.StartAsync();
                MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 1);
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");
                var blobB = await GetBlobAsync(Options.Value.VerifiedPackageContainerName, "latest_verified_packages.csv.gz");
                var propertiesB = await blobB.GetPropertiesAsync();
                Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
            }
        }

        public class VerifiedPackagesToCsv_NonExistentId : VerifiedPackagesToCsvIntegrationTest
        {
            public VerifiedPackagesToCsv_NonExistentId(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
                await service.InitializeAsync();
                await service.StartAsync();
                MockVersionSet.Setup(x => x.DidIdEverExist("Knapcode.TorSharp")).Returns(false);
                MockVersionSet.Setup(x => x.DidIdEverExist("Newtonsoft.Json")).Returns(false);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step1, "verified_packages_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step1, "latest_verified_packages.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync();
                MockVersionSet.Setup(x => x.DidIdEverExist("Knapcode.TorSharp")).Returns(true);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 3);
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step1, "verified_packages_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step2, "verified_packages_08585908696854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_NonExistentIdDir, Step2, "latest_verified_packages.csv.gz");
            }
        }

        public class VerifiedPackagesToCsv_UncheckedId : VerifiedPackagesToCsvIntegrationTest
        {
            public VerifiedPackagesToCsv_UncheckedId(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
                await service.InitializeAsync();
                await service.StartAsync();
                MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_UncheckedIdDir, Step1, "verified_packages_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(VerifiedPackagesToCsv_UncheckedIdDir, Step1, "latest_verified_packages.csv.gz");
            }
        }

        public class VerifiedPackagesToCsv_JustLatest : VerifiedPackagesToCsvIntegrationTest
        {
            public VerifiedPackagesToCsv_JustLatest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task ExecuteAsync()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step1, "latest_verified_packages.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.VerifiedPackageContainerName, 1);
                await AssertCsvBlobAsync(VerifiedPackagesToCsvDir, Step2, "latest_verified_packages.csv.gz");
            }
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<VerifiedPackage>> service)
        {
            await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());
        }

        private void ConfigureAndSetLastModified()
        {
            ConfigureSettings = x => x.VerifiedPackagesV1Url = $"http://localhost/{TestData}/{VerifiedPackagesToCsvDir}/verifiedPackages.json";

            // Set the Last-Modified date
            var fileA = new FileInfo(Path.Combine(TestData, VerifiedPackagesToCsvDir, Step1, "verifiedPackages.json"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
            };
            var fileB = new FileInfo(Path.Combine(TestData, VerifiedPackagesToCsvDir, Step2, "verifiedPackages.json"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-15T19:00:00Z")
            };

            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async req =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/verifiedPackages.json"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestData}/{VerifiedPackagesToCsvDir}/{stepName}/verifiedPackages.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvBlobAsync<VerifiedPackageRecord>(Options.Value.VerifiedPackageContainerName, testName, stepName, "latest_verified_packages.csv", blobName);
        }
    }
}
