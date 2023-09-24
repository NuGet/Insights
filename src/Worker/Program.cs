// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

#nullable enable

namespace NuGet.Insights.Worker
{
    public static class Program
    {
        static Program()
        {
            HomeTempStreamDirectory = GetHomeTempStreamDirectory();
        }

        private static TempStreamDirectory? HomeTempStreamDirectory { get; }

        private static void Main()
        {
            new HostBuilder()
                .ConfigureNuGetInsightsWorker()
                .Build()
                .Run();
        }

        public static IHostBuilder ConfigureNuGetInsightsWorker(this IHostBuilder builder)
        {
            return builder
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();

                    HandleMoveTempToHome(hostContext);

                    services
                        .AddOptions<NuGetInsightsSettings>()
                        .Configure<IConfiguration>((settings, configuration) =>
                        {
                            configuration.GetSection(NuGetInsightsSettings.DefaultSectionName).Bind(settings);
                            Configure(settings);
                        });

                    services
                        .AddOptions<NuGetInsightsWorkerSettings>()
                        .Configure<IConfiguration>((settings, configuration) =>
                        {
                            configuration.GetSection(NuGetInsightsSettings.DefaultSectionName).Bind(settings);
                            Configure(settings);
                        });

                    services.AddNuGetInsights("NuGet.Insights.Worker");
                    services.AddNuGetInsightsWorker();
                })
                .ConfigureLogging(logging =>
                {
                    logging.Services.Configure<LoggerFilterOptions>(options =>
                    {
                        options.Rules.Remove(options
                            .Rules
                            .Single(rule => rule.ProviderName == typeof(ApplicationInsightsLoggerProvider).FullName));
                    });
                });
        }

        private static void HandleMoveTempToHome(HostBuilderContext context)
        {
            var settings = context
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

        private static TempStreamDirectory? GetHomeTempStreamDirectory()
        {
            if (DoesHomeExist())
            {
                var networkDir = Environment.ExpandEnvironmentVariables(Path.Combine("%HOME%", "NuGet.Insights", "home"));
                return new TempStreamDirectory
                {
                    Path = networkDir,
                    MaxConcurrentWriters = 32,
                    BufferSize = 4 * 1024 * 1024,
                };
            }

            return null;
        }

        private static bool DoesHomeExist()
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            return !string.IsNullOrWhiteSpace(home) && Directory.Exists(home);
        }

        private static void Configure(NuGetInsightsSettings settings)
        {
            if (HomeTempStreamDirectory is not null)
            {
                settings.TempDirectories.Add(HomeTempStreamDirectory);
            }
        }
    }
}
