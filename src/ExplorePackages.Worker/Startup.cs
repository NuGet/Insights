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
            FixCustomMetrics(builder);
            HandleMoveTempToHome(builder);

            AddOptions<ExplorePackagesSettings>(builder, ExplorePackagesSettings.DefaultSectionName);
            AddOptions<ExplorePackagesWorkerSettings>(builder, ExplorePackagesSettings.DefaultSectionName);

            builder.Services.Configure<ExplorePackagesSettings>(Configure);
            builder.Services.Configure<ExplorePackagesWorkerSettings>(Configure);

            builder.Services.AddExplorePackages("Knapcode.ExplorePackages.Worker");
            builder.Services.AddExplorePackagesWorker();

            builder.Services.AddSingleton<IQueueProcessorFactory, UnencodedQueueProcessorFactory>();
            builder.Services.AddSingleton<ITelemetryClient, TelemetryClientWrapper>();
        }

        private static void FixCustomMetrics(IFunctionsHostBuilder builder)
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
        }

        private static void HandleMoveTempToHome(IFunctionsHostBuilder builder)
        {
            var settings = builder
                .GetContext()
                .Configuration?
                .GetSection(ExplorePackagesSettings.DefaultSectionName)
                .Get<ExplorePackagesWorkerSettings>();

            if (settings?.MoveTempToHome == true)
            {
                if (!DoesHomeExist())
                {
                    throw new InvalidOperationException("The HOME environment variable does not point to an existing directory.");
                }

                var newTemp = Environment.ExpandEnvironmentVariables(Path.Combine("%HOME%", "Knapcode.ExplorePackages", "temp"));
                if (!Directory.Exists(newTemp))
                {
                    Directory.CreateDirectory(newTemp);
                }

                Environment.SetEnvironmentVariable("TMP", newTemp);
                Environment.SetEnvironmentVariable("TEMP", newTemp);
            }
        }

        private static void Configure(ExplorePackagesSettings settings)
        {
            if (DoesHomeExist())
            {
                var networkDir = Environment.ExpandEnvironmentVariables(Path.Combine("%HOME%", "Knapcode.ExplorePackages", "home"));
                settings.TempDirectories.Add(new TempStreamDirectory
                {
                    Path = networkDir,
                    MaxConcurrentWriters = 32,
                    BufferSize = 4 * 1024 * 1024,
                });
            }
        }

        private static bool DoesHomeExist()
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            return !string.IsNullOrWhiteSpace(home) && Directory.Exists(home);
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
