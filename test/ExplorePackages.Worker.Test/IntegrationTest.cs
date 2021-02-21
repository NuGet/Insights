using System;
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

            // Arrange
            await CatalogScanService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

            await SetCursorAsync(CatalogScanDriverType.LoadPackageFile, min0);
            await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, min0);
            await SetCursorAsync(CatalogScanDriverType.FindPackageAssembly, min0);
            await SetCursorAsync(CatalogScanDriverType.FindPackageAsset, min0);
            await SetCursorAsync(CatalogScanDriverType.FindPackageSignature, min0);

            // Act
            var loadPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageManifest, max1, onlyLatestLeaves: null);
            await UpdateAsync(loadPackageManifest.Scan);
            var startingNuspecRequestCount = GetNuspecRequestCount();

            var loadPackageFile = await CatalogScanService.UpdateAsync(CatalogScanDriverType.LoadPackageFile, max1, onlyLatestLeaves: null);
            await UpdateAsync(loadPackageFile.Scan);

            var findPackageAssembly = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageAssembly, max1, onlyLatestLeaves: null);
            await UpdateAsync(findPackageAssembly.Scan);

            var startingNupkgRequestCount = GetNupkgRequestCount();

            var findPackageAsset = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageAsset, max1, onlyLatestLeaves: null);
            var findPackageSignature = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageSignature, max1, onlyLatestLeaves: null);
            await UpdateAsync(findPackageAsset.Scan);
            await UpdateAsync(findPackageSignature.Scan);

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
