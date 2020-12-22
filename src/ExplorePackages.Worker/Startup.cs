using System;
using System.IO;
using System.Linq;
using Knapcode.ExplorePackages.Worker;
using Microsoft.ApplicationInsights.Extensibility;
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
            var serviceDescriptor = builder.Services.SingleOrDefault(tc => tc.ServiceType == typeof(TelemetryConfiguration));
            if (serviceDescriptor?.ImplementationFactory != null)
            {
                var factory = serviceDescriptor.ImplementationFactory;
                builder.Services.Remove(serviceDescriptor);
                builder.Services.AddSingleton(provider =>
                {
                    if (factory.Invoke(provider) is TelemetryConfiguration config)
                    {
                        config.TelemetryInitializers.Add(new RemoveLogLevelFromMetricsTelemetryInitializer());

                        return config;
                    }
                    return null;
                });
            }

            AddOptions<ExplorePackagesSettings>(builder, ExplorePackagesSettings.DefaultSectionName);
            AddOptions<ExplorePackagesWorkerSettings>(builder, ExplorePackagesSettings.DefaultSectionName);

            builder.Services.Configure<ExplorePackagesWorkerSettings>(settings =>
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME")))
                {
                    settings.TempBaseDir = Environment.ExpandEnvironmentVariables(Path.Combine("%HOME%", "Knapcode.ExplorePackages", "temp"));
                }
            });

            builder.Services.AddExplorePackages("Knapcode.ExplorePackages.Worker");
            builder.Services.AddExplorePackagesWorker();

            builder.Services.AddSingleton<IQueueProcessorFactory, UnencodedQueueProcessorFactory>();
            builder.Services.AddSingleton<ITelemetryClient, TelemetryClientWrapper>();
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
