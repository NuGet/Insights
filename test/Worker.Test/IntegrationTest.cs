using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues.Models;
using Knapcode.ExplorePackages.Worker.DownloadsToCsv;
using Knapcode.ExplorePackages.Worker.KustoIngestion;
using Knapcode.ExplorePackages.Worker.OwnersToCsv;
using Knapcode.ExplorePackages.Worker.StreamWriterUpdater;
using Kusto.Ingest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Queue;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class IntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public IntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            hostBuilder
                .ConfigureWebJobs(new Startup().Configure)
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient(s => Output.GetTelemetryClient());
                    serviceCollection.AddTransient<Functions>();

                    serviceCollection.Configure((Action<ExplorePackagesSettings>)ConfigureDefaultsAndSettings);
                    serviceCollection.Configure((Action<ExplorePackagesWorkerSettings>)ConfigureWorkerDefaultsAndSettings);
                });
        }

        protected override async Task ProcessMessageAsync(IServiceProvider serviceProvider, QueueType queueType, QueueMessage message)
        {
            var functions = serviceProvider.GetRequiredService<Functions>();
            var cloudMessage = new CloudQueueMessage(message.Body.ToString());
            switch (queueType)
            {
                case QueueType.Work:
                    await functions.WorkQueueAsync(cloudMessage);
                    break;
                case QueueType.Expand:
                    await functions.ExpandQueueAsync(cloudMessage);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public class CanRunTimersAsync : IntegrationTest
        {
            public CanRunTimersAsync(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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

            [Fact]
            public async Task Execute()
            {
                ConfigureSettings = x =>
                {
                    x.DownloadsV1Url = $"http://localhost/{TestData}/DownloadsToCsv/{Step1}/downloads.v1.json";
                    x.OwnersV2Url = $"http://localhost/{TestData}/OwnersToCsv/{Step1}/owners.v2.json";
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AutoStartDownloadToCsv = true;
                    x.AutoStartOwnersToCsv = true;
                };

                // Arrange
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsoluteUri == Options.Value.DownloadsV1Url
                     || req.RequestUri.AbsoluteUri == Options.Value.OwnersV2Url)
                    {
                        return await TestDataHttpClient.SendAsync(Clone(req));
                    }

                    return null;
                };

                var service = Host.Services.GetRequiredService<TimerExecutionService>();
                await service.InitializeAsync();

                // Act
                using (var scope = Host.Services.CreateScope())
                {
                    await scope
                        .ServiceProvider
                        .GetRequiredService<Functions>()
                        .TimerAsync(timerInfo: null);
                }

                await ProcessQueueAsync(() => { }, () => Task.FromResult(true));

                // Assert
                await AssertBlobCountAsync(Options.Value.PackageDownloadsContainerName, 1);
                await AssertBlobCountAsync(Options.Value.PackageOwnersContainerName, 1);
            }
        }

        public class CanRunAllCatalogScansAndKustoIngestionAsync : IntegrationTest
        {
            public CanRunAllCatalogScansAndKustoIngestionAsync(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
            {
                base.ConfigureHostBuilder(hostBuilder);

                hostBuilder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient(x => MockCslAdminProvider.Object);
                    serviceCollection.AddTransient(x => MockKustoQueueIngestClient.Object);
                });
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureSettings = x =>
                {
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                    x.DownloadsV1Url = $"http://localhost/{TestData}/{DownloadsToCsvIntegrationTest.DownloadsToCsvDir}/downloads.v1.json";
                    x.OwnersV2Url = $"http://localhost/{TestData}/{OwnersToCsvIntegrationTest.OwnersToCsvDir}/owners.v2.json";
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AppendResultStorageBucketCount = 1;
                };
                HttpMessageHandlerFactory.OnSendAsync = async req =>
                {
                    if (req.RequestUri.AbsolutePath.EndsWith("/downloads.v1.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{DownloadsToCsvIntegrationTest.DownloadsToCsvDir}/{Step1}/downloads.v1.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    if (req.RequestUri.AbsolutePath.EndsWith("/owners.v2.json"))
                    {
                        var newReq = Clone(req);
                        newReq.RequestUri = new Uri($"http://localhost/{TestData}/{OwnersToCsvIntegrationTest.OwnersToCsvDir}/{Step1}/owners.v2.json");
                        return await TestDataHttpClient.SendAsync(newReq);
                    }

                    return null;
                };

                // Arrange
                await CatalogScanService.InitializeAsync();

                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    await SetCursorAsync(type, min0);
                }

                // Act - catalog scans
                await CatalogScanService.UpdateAllAsync(max1);
                var attempts = 0;
                await ProcessQueueAsync(
                    () => { },
                    async () =>
                    {
                        var indexScans = await CatalogScanStorageService.GetIndexScansAsync();
                        if (indexScans.All(x => x.State == CatalogIndexScanState.Complete))
                        {
                            return true;
                        }

                        attempts++;
                        if (attempts > 30)
                        {
                            return true;
                        }

                        await Task.Delay(1000);

                        return false;
                    });

                // Assert
                // Make sure all scans completed.
                var indexScans = await CatalogScanStorageService.GetIndexScansAsync();
                Assert.All(indexScans, x => Assert.Equal(CatalogIndexScanState.Complete, x.State));
                Assert.Equal(
                    CatalogScanCursorService.StartableDriverTypes.ToArray(),
                    indexScans.Select(x => x.DriverType).OrderBy(x => x).ToArray());

                // Act - owners and downloads to CSV
                await ProcessStreamWriterUpdaterAsync<PackageDownloadSet>();
                await ProcessStreamWriterUpdaterAsync<PackageOwnerSet>();

                // Act - Kusto ingestion
                await KustoIngestionService.InitializeAsync();
                var ingestion = await KustoIngestionService.StartAsync();
                ingestion = await UpdateAsync(ingestion);

                // Make sure all of the containers are have ingestions
                var containerNames = Host.Services.GetRequiredService<CsvResultStorageContainers>().GetContainerNames();
                foreach (var containerName in containerNames)
                {
                    MockKustoQueueIngestClient.Verify(x => x.IngestFromStorageAsync(
                        It.Is<string>(y => y.Contains(containerName)),
                        It.IsAny<KustoIngestionProperties>(),
                        It.IsAny<StorageSourceOptions>()));
                }
            }

            private async Task ProcessStreamWriterUpdaterAsync<T>()
            {
                var service = Host.Services.GetRequiredService<IStreamWriterUpdaterService<T>>();
                await service.InitializeAsync();
                await service.StartAsync();
                await ProcessQueueAsync(() => { }, async () => !await service.IsRunningAsync());
            }
        }

        public class DownloadsNupkgsAndNuspecsInTheExpectedDrivers : IntegrationTest
        {
            public DownloadsNupkgsAndNuspecsInTheExpectedDrivers(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureSettings = x =>
                {
                    x.MaxTempMemoryStreamSize = 0;
                    x.TempDirectories[0].MaxConcurrentWriters = 1;
                };
                ConfigureWorkerSettings = x =>
                {
                    x.AppendResultStorageBucketCount = 1;
                };

                // Arrange
                await CatalogScanService.InitializeAsync();

                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    await SetCursorAsync(type, min0);
                }

                // Act

                // Load the manifests
                var loadPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageManifest, max1);
                await UpdateAsync(loadPackageManifest.Scan);

                var startingNuspecRequestCount = GetNuspecRequestCount();

                // Load latest package leaves
                var loadLatestPackageLeaf = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadLatestPackageLeaf, max1);
                await UpdateAsync(loadLatestPackageLeaf.Scan);

                Assert.Equal(0, GetNupkgRequestCount());

                // Load the packages, process package assemblies, and run NuGet Package Explorer.
                var loadPackageArchive = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await UpdateAsync(loadPackageArchive.Scan);
                var packageAssemblyToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.PackageAssemblyToCsv, max1);
                var nuGetPackageExplorerToCsv = await CatalogScanService.UpdateAsync(CatalogScanDriverType.NuGetPackageExplorerToCsv, max1);
                await UpdateAsync(packageAssemblyToCsv.Scan);
                await UpdateAsync(nuGetPackageExplorerToCsv.Scan);

                var startingNupkgRequestCount = GetNupkgRequestCount();

                // Load the versions
                var loadPackageVersion = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageVersion, max1);
                await UpdateAsync(loadPackageVersion.Scan);

                // Start all of the scans
                var startedScans = new List<CatalogIndexScan>();
                foreach (var type in CatalogScanCursorService.StartableDriverTypes)
                {
                    var startedScan = await CatalogScanService.UpdateAsync(type, max1);
                    if (startedScan.Type == CatalogScanServiceResultType.FullyCaughtUpWithMax)
                    {
                        continue;
                    }
                    Assert.Equal(CatalogScanServiceResultType.NewStarted, startedScan.Type);
                    startedScans.Add(startedScan.Scan);
                }

                // Wait for all of the scans to complete.
                foreach (var scan in startedScans)
                {
                    await UpdateAsync(scan);
                }

                var finalNupkgRequestCount = GetNupkgRequestCount();
                var finalNuspecRequestCount = GetNuspecRequestCount();

                // Assert
                var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
                foreach (var queue in Enum.GetValues(typeof(QueueType)).Cast<QueueType>())
                {
                    Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync(queue));
                    Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(queue, 32));
                    Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync(queue));
                    Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(queue, 32));
                }

                Assert.NotEqual(0, startingNupkgRequestCount);
                Assert.NotEqual(0, startingNuspecRequestCount);
                Assert.Equal(startingNupkgRequestCount, finalNupkgRequestCount);
                Assert.Equal(startingNuspecRequestCount, finalNuspecRequestCount);

                var userAgents = HttpMessageHandlerFactory.Requests.Select(r => r.Headers.UserAgent.ToString()).Distinct();
                var userAgent = Assert.Single(userAgents);
                Assert.StartsWith("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", userAgent);
                Assert.Matches(@"(NuGet Test Client)/?(\d+)?\.?(\d+)?\.?(\d+)?", userAgent);
            }
        }

        private int GetNuspecRequestCount()
        {
            return HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri.EndsWith(".nuspec"));
        }

        private int GetNupkgRequestCount()
        {
            return HttpMessageHandlerFactory.Requests.Count(x => x.RequestUri.AbsoluteUri.EndsWith(".nupkg"));
        }
    }
}
