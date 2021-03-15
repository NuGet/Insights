using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Queue;
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

        protected override async Task ProcessMessageAsync(IServiceProvider serviceProvider, CloudQueueMessage message)
        {
            await serviceProvider
                .GetRequiredService<Functions>()
                .WorkerQueueAsync(message);
        }

        public class CanRunTimersAsync : IntegrationTest
        {
            public CanRunTimersAsync(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureSettings = x =>
                {
                    x.DownloadsV1Url = $"http://localhost/{TestData}/DownloadsToCsv/{Step1}/downloads.v1.json";
                    x.OwnersV2Url = $"http://localhost/{TestData}/OwnersToCsv/{Step1}/owners.v2.json";
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

        public class CanRunAllCatalogScansAsync : IntegrationTest
        {
            public CanRunAllCatalogScansAsync(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
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
                await CatalogScanService.UpdateAllAsync(max1);
                var attempts = 0;
                await ProcessQueueAsync(
                    () => { },
                    async () =>
                    {
                        var indexScans = await CatalogScanStorageService.GetIndexScans();
                        if (indexScans.All(x => x.ParsedState == CatalogIndexScanState.Complete))
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

                // Make sure all scans completed.
                var indexScans = await CatalogScanStorageService.GetIndexScans();
                Assert.All(indexScans, x => Assert.Equal(CatalogIndexScanState.Complete, x.ParsedState));
                Assert.Equal(
                    CatalogScanCursorService.StartableDriverTypes.ToArray(),
                    indexScans.Select(x => x.ParsedDriverType).OrderBy(x => x).ToArray());
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
                Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync());
                Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(32));
                Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync());
                Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(32));

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
