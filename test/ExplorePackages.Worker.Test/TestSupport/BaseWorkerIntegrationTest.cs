using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage.Queue;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public abstract class BaseWorkerIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        public BaseWorkerIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override IHostBuilder ConfigureHostBuilder(IHostBuilder hostBuilder, ITestOutputHelper output)
        {
            var startup = new Startup();
            return base.ConfigureHostBuilder(hostBuilder
                .ConfigureWebJobs(startup.Configure)
                .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddTransient<WorkerQueueFunction>();
                }), output);
        }

        protected override async Task ProcessMessageAsync(IServiceProvider serviceProvider, CloudQueueMessage message)
        {
            var target = serviceProvider.GetRequiredService<WorkerQueueFunction>();
            await target.ProcessAsync(message);
        }
    }
}
