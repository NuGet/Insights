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
            // Arrange
            await CatalogScanService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

            await SetCursorAsync(CatalogScanDriverType.FindPackageFile, min0);
            await SetCursorAsync(CatalogScanDriverType.FindPackageManifest, min0);
            await SetCursorAsync(CatalogScanDriverType.FindPackageAsset, min0);
            await SetCursorAsync(CatalogScanDriverType.FindPackageSignature, min0);

            // Act
            var findPackageFile = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageFile, max1, onlyLatestLeaves: null);
            await UpdateAsync(findPackageFile);
            var findPackageFileNupkgRequestCount = GetNupkgRequestCount();

            var findPackageManifest = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageManifest, max1, onlyLatestLeaves: null);
            await UpdateAsync(findPackageManifest);
            var findPackageManifestNuspecRequestCount = GetNuspecRequestCount();

            var findPackageAsset = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageAsset, max1, onlyLatestLeaves: null);
            var findPackageSignature = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindPackageSignature, max1, onlyLatestLeaves: null);
            await UpdateAsync(findPackageAsset);
            await UpdateAsync(findPackageSignature);
            var finalFileRequestNupkgCount = GetNupkgRequestCount();
            var finalFileRequestNuspecCount = GetNuspecRequestCount();

            // Assert
            var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
            Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync());
            Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(32));
            Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync());
            Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(32));

            Assert.NotEqual(0, findPackageFileNupkgRequestCount);
            Assert.NotEqual(0, findPackageManifestNuspecRequestCount);
            Assert.Equal(findPackageFileNupkgRequestCount, finalFileRequestNupkgCount);
            Assert.Equal(findPackageManifestNuspecRequestCount, finalFileRequestNuspecCount);

            var userAgents = HttpMessageHandlerFactory.Requests.Select(r => r.Headers.UserAgent.ToString()).Distinct();
            Assert.All(userAgents, ua => Assert.StartsWith("Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; Trident/5.0; AppInsights)", ua));
            Assert.All(userAgents, ua => Assert.Matches(@"(NuGet Test Client)/?(\d+)?\.?(\d+)?\.?(\d+)?", ua.ToString()));
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
