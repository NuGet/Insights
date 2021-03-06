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
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder
                .ConfigureWebJobs(new Startup().Configure)
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient(s => Output.GetTelemetryClient());
                    serviceCollection.AddTransient<WorkerQueueFunction>();

                    serviceCollection.Configure((Action<ExplorePackagesSettings>)ConfigureDefaultsAndSettings);
                    serviceCollection.Configure((Action<ExplorePackagesWorkerSettings>)ConfigureWorkerDefaultsAndSettings);
                });
        }

        protected override async Task ProcessMessageAsync(IServiceProvider serviceProvider, CloudQueueMessage message)
        {
            var target = serviceProvider.GetRequiredService<WorkerQueueFunction>();
            await target.ProcessAsync(message);
        }

        [Fact]
        public async Task ProcessesMessageAsync()
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

            var driverTypes = Enum
                .GetValues(typeof(CatalogScanDriverType))
                .Cast<CatalogScanDriverType>()
                .Where(x => x != CatalogScanDriverType.Internal_FindLatestCatalogLeafScan
                         && x != CatalogScanDriverType.Internal_FindLatestCatalogLeafScanPerId)
                .ToList();
            foreach (var type in driverTypes)
            {
                await SetCursorAsync(type, min0);
            }

            // Act

            // Load the manifests
            var loadPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageManifest, max1);
            await UpdateAsync(loadPackageManifest.Scan);
            var startingNuspecRequestCount = GetNuspecRequestCount();

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
            foreach (var type in driverTypes)
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
