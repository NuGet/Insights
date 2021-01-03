using System;
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

            await SetCursorAsync(CatalogScanDriverType.FindLatestPackageLeaf, min0);
            var initial = await CatalogScanService.UpdateAsync(CatalogScanDriverType.FindLatestPackageLeaf, max1, onlyLatestLeaves: null);

            // Act
            await UpdateAsync(initial);

            // Assert
            var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
            Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync());
            Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(32));
            Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync());
            Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(32));
        }
    }
}
