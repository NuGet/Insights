using Knapcode.ExplorePackages.Worker;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Knapcode.ExplorePackages.Worker
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            AddOptions<ExplorePackagesSettings>(builder, ExplorePackagesSettings.DefaultSectionName);
            AddOptions<ExplorePackagesWorkerSettings>(builder, ExplorePackagesSettings.DefaultSectionName);

            builder.Services.AddExplorePackages("Knapcode.ExplorePackages.Worker");
            builder.Services.AddExplorePackagesWorker();

            builder.Services.AddSingleton<IQueueProcessorFactory, UnencodedQueueProcessorFactory>();
            builder.Services.AddTransient<ITelemetryClient, TelemetryClientWrapper>();
        }

        private static void AddOptions<TOptions>(IFunctionsHostBuilder builder, string sectionName) where TOptions : class
        {
            builder
                .Services
                .AddOptions<TOptions>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection(sectionName).Bind(settings);
                });
        }
    }
}
