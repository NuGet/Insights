using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class WorkerQueueFunctionIntegrationTest : BaseWorkerIntegrationTest
    {
        public WorkerQueueFunctionIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task ProcessesMessageAsync()
        {
            // Arrange
            await CatalogScanService.InitializeAsync();

            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");

            await SetCursorAsync(CatalogScanType.FindLatestLeaves, min0);
            var initial = await CatalogScanService.UpdateAsync(CatalogScanType.FindLatestLeaves, max1);

            // Act
            await ProcessQueueAsync();

            // Assert
            var final = await CatalogScanStorageService.GetIndexScanAsync(initial.CursorName, initial.ScanId);
            Assert.Equal(CatalogScanState.Complete, final.ParsedState);

            var rawMessageEnqueuer = Host.Services.GetRequiredService<IRawMessageEnqueuer>();
            Assert.Equal(0, await rawMessageEnqueuer.GetApproximateMessageCountAsync());
            Assert.Equal(0, await rawMessageEnqueuer.GetAvailableMessageCountLowerBoundAsync(32));
            Assert.Equal(0, await rawMessageEnqueuer.GetPoisonApproximateMessageCountAsync());
            Assert.Equal(0, await rawMessageEnqueuer.GetPoisonAvailableMessageCountLowerBoundAsync(32));
        }
    }
}
