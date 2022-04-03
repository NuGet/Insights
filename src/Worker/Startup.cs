// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.Worker;

[assembly: FunctionsStartup(typeof(Startup))]

namespace NuGet.Insights.Worker
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            FixCustomMetrics(builder);
            HandleMoveTempToHome(builder);

            AddOptions<NuGetInsightsSettings>(builder, NuGetInsightsSettings.DefaultSectionName);
            AddOptions<NuGetInsightsWorkerSettings>(builder, NuGetInsightsSettings.DefaultSectionName);

            builder.Services.Configure<NuGetInsightsSettings>(Configure);
            builder.Services.Configure<NuGetInsightsWorkerSettings>(Configure);

            builder.Services.AddNuGetInsights("NuGet.Insights.Worker");
            builder.Services.AddNuGetInsightsWorker();

            builder.Services.AddSingleton<INameResolver, CustomNameResolver>();

            for (var i = 0; i < builder.Services.Count; i++)
            {
                if (builder.Services[i].ImplementationType == typeof(TelemetryClient))
                {
                    builder.Services.AddSingleton<ITelemetryClient, TelemetryClientWrapper>();
                    break;
                }
            }
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
                .GetSection(NuGetInsightsSettings.DefaultSectionName)
                .Get<NuGetInsightsWorkerSettings>();

            if (settings?.MoveTempToHome == true)
            {
                if (!DoesHomeExist())
                {
                    throw new InvalidOperationException("The HOME environment variable does not point to an existing directory.");
                }

                var newTemp = Environment.ExpandEnvironmentVariables(Path.Combine("%HOME%", "NuGet.Insights", "temp"));
                if (!Directory.Exists(newTemp))
                {
                    Directory.CreateDirectory(newTemp);
                }

                Environment.SetEnvironmentVariable("TMP", newTemp);
                Environment.SetEnvironmentVariable("TEMP", newTemp);
            }
        }

        private static void Configure(NuGetInsightsSettings settings)
        {
            if (DoesHomeExist())
            {
                var networkDir = Environment.ExpandEnvironmentVariables(Path.Combine("%HOME%", "NuGet.Insights", "home"));
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
