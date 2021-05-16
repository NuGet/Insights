using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionTimerTest : BaseWorkerLogicIntegrationTest
    {
        public KustoIngestionTimerTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient<KustoIngestionTimer>();
            });
        }

        public KustoIngestionTimer Target => Host.Services.GetRequiredService<KustoIngestionTimer>();

        [Fact]
        public async Task StartsKustoIngestionAsync()
        {
            await Target.InitializeAsync();

            var result = await Target.ExecuteAsync();

            var ingestions = await KustoIngestionStorageService.GetIngestionsAsync();
            Assert.Single(ingestions);
            Assert.True(result);
        }

        [Fact]
        public async Task DoesNotStartKustoIngestionWhenIngestionIsAlreadyRunningAsync()
        {
            await Target.InitializeAsync();
            await KustoIngestionService.StartAsync();

            var result = await Target.ExecuteAsync();

            var ingestions = await KustoIngestionStorageService.GetIngestionsAsync();
            Assert.Single(ingestions);
            Assert.False(result);
        }
    }
}
