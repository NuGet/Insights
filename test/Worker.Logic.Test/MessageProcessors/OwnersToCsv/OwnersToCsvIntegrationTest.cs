// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.OwnersToCsv
{
    public class OwnersToCsvIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public const string OwnersToCsvDir = nameof(OwnersToCsv);
        private const string OwnersToCsv_NonExistentIdDir = nameof(OwnersToCsvDir_NonExistentId);
        private const string OwnersToCsv_UncheckedIdDir = nameof(OwnersToCsvDir_UncheckedId);

        public OwnersToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

        public class OwnersToCsv : OwnersToCsvIntegrationTest
        {
            public OwnersToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 3);
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "owners_08585908696854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "latest_owners.csv.gz");
            }
        }

        public class OwnersToCsv_NoOp : OwnersToCsvIntegrationTest
        {
            public OwnersToCsv_NoOp(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
                var blobA = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
                var propertiesA = await blobA.GetPropertiesAsync();

                // Arrange
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
                var blobB = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
                var propertiesB = await blobB.GetPropertiesAsync();
                Assert.Equal(propertiesA.Value.ETag, propertiesB.Value.ETag);
            }
        }

        public class OwnersToCsv_DifferentVersionSet : OwnersToCsvIntegrationTest
        {
            public OwnersToCsv_DifferentVersionSet(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
                var blobA = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
                var propertiesA = await blobA.GetPropertiesAsync();

                // Arrange
                await service.StartAsync();
                MockVersionSet.Setup(x => x.CommitTimestamp).Returns(new DateTimeOffset(2021, 5, 10, 12, 15, 30, TimeSpan.Zero));

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");
                var blobB = await GetBlobAsync(Options.Value.PackageOwnerContainerName, "latest_owners.csv.gz");
                var propertiesB = await blobB.GetPropertiesAsync();
                Assert.NotEqual(propertiesA.Value.ETag, propertiesB.Value.ETag);
            }
        }

        public class OwnersToCsvDir_NonExistentId : OwnersToCsvIntegrationTest
        {
            public OwnersToCsvDir_NonExistentId(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
                await service.InitializeAsync();
                await service.StartAsync();
                string id;
                MockVersionSet.Setup(x => x.TryGetId("Knapcode.TorSharp", out id)).Returns(false);
                MockVersionSet.Setup(x => x.TryGetId("Newtonsoft.Json", out id)).Returns(false);

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1, "latest_owners.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync();
                MockVersionSet
                    .Setup(x => x.TryGetId("Knapcode.TorSharp", out id))
                    .Returns(true)
                    .Callback(new TryGetId((string id, out string outId) => outId = "knapcode.TORSHARP"));

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 3);
                await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step2, "owners_08585908696854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsv_NonExistentIdDir, Step2, "latest_owners.csv.gz");
            }
        }

        public class OwnersToCsvDir_UncheckedId : OwnersToCsvIntegrationTest
        {
            public OwnersToCsvDir_UncheckedId(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureWorkerSettings = x => x.OnlyKeepLatestInAuxiliaryFileUpdater = false;
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
                await service.InitializeAsync();
                await service.StartAsync();
                MockVersionSet.Setup(x => x.GetUncheckedIds()).Returns(new[] { "UncheckedB", "UncheckedA" });

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsv_UncheckedIdDir, Step1, "owners_08585909596854775807.csv.gz");
                await AssertCsvBlobAsync(OwnersToCsv_UncheckedIdDir, Step1, "latest_owners.csv.gz");
            }
        }

        public class OwnersToCsv_JustLatest : OwnersToCsvIntegrationTest
        {
            public OwnersToCsv_JustLatest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                ConfigureAndSetLastModified();
                var service = Host.Services.GetRequiredService<IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>>>();
                await service.InitializeAsync();
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertCsvBlobAsync(OwnersToCsvDir, Step1, "latest_owners.csv.gz");

                // Arrange
                SetData(Step2);
                await service.StartAsync();

                // Act
                await ProcessQueueAsync(service);

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageOwnerContainerName, 1);
                await AssertCsvBlobAsync(OwnersToCsvDir, Step2, "latest_owners.csv.gz");
            }
        }

        private async Task ProcessQueueAsync(IAuxiliaryFileUpdaterService<AsOfData<PackageOwner>> service)
        {
            await ProcessQueueAsync(async () => !await service.IsRunningAsync());
        }

        private void ConfigureAndSetLastModified()
        {
            ConfigureSettings = x => x.OwnersV2Urls = new List<string> { $"http://localhost/{TestData}/{OwnersToCsvDir}/owners.v2.json" };

            // Set the Last-Modified date
            var fileA = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step1, "owners.v2.json"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-14T18:00:00Z")
            };
            var fileB = new FileInfo(Path.Combine(TestData, OwnersToCsvDir, Step2, "owners.v2.json"))
            {
                LastWriteTimeUtc = DateTime.Parse("2021-01-15T19:00:00Z")
            };

            SetData(Step1);
        }

        private void SetData(string stepName)
        {
            HttpMessageHandlerFactory.OnSendAsync = async (req, _, _) =>
            {
                if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                {
                    var newReq = Clone(req);
                    newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvDir}/{stepName}/owners.v2.json");
                    return await TestDataHttpClient.SendAsync(newReq);
                }

                return null;
            };
        }

        private Task AssertCsvBlobAsync(string testName, string stepName, string blobName)
        {
            return AssertCsvBlobAsync<PackageOwnerRecord>(Options.Value.PackageOwnerContainerName, testName, stepName, "latest_owners.csv", blobName);
        }
    }
}
