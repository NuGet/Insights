// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using McMaster.Extensions.CommandLineUtils;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using NuGet.Insights.Website;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Tool
{
    public class Program
    {
        private static readonly IReadOnlyDictionary<string, Type> Commands = new Dictionary<string, Type>
        {
            { "analyze-http-cache", typeof(AnalyzeHttpCacheCommand) },
            { "process-messages", typeof(ProcessMessagesCommand) },
            { "ingest-downloads-json", typeof(IngestDownloadsJsonCommand) },
            { "sandbox", typeof(SandboxCommand) },
        };

        public static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> MainAsync(string[] args)
        {
            // Initialize the dependency injection container.
            var serviceCollection = InitializeServiceCollection();
            await using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                // Set up the cancel event to release the lease if someone hits Ctrl + C while the program is running.
                var cancelEvent = new SemaphoreSlim(0);
                var cancellationTokenSource = new CancellationTokenSource();
                var cancelled = 0;
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                    {
                        eventArgs.Cancel = true;
                        cancellationTokenSource.Cancel();
                        cancelEvent.Release();
                    }
                };

                // Wait for cancellation and execute the program in parallel.
                var cancelTask = WaitForCancellationAsync(cancelEvent, serviceProvider);
                var executeTask = ExecuteAsync(args, serviceProvider, cancellationTokenSource.Token);

                return await await Task.WhenAny(cancelTask, executeTask);
            }
        }

        private static async Task<int> WaitForCancellationAsync(
            SemaphoreSlim cancelEvent,
            IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            await cancelEvent.WaitAsync();
            logger.LogWarning("Cancelling...");

            return 1;
        }

        public static async Task<int> ExecuteAsync(
            string[] args,
            IServiceProvider serviceProvider,
            CancellationToken token)
        {
            await Task.Yield();

            var app = new CommandLineApplication();
            app.HelpOption();
            app.OnExecute(() => app.ShowHelp());

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            foreach (var pair in Commands)
            {
                AddCommand(pair.Value, serviceProvider, app, pair.Key, logger, token);
            }

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected exception occured.");
                return 1;
            }
        }

        private static void AddCommand(
            Type commandType,
            IServiceProvider serviceProvider,
            CommandLineApplication app,
            string commandName,
            ILogger logger,
            CancellationToken token)
        {
            var command = (ICommand)serviceProvider.GetRequiredService(commandType);

            app.Command(
                commandName,
                x =>
                {
                    x.HelpOption();

                    var debugOption = x.Option(
                        "--debug",
                        "Launch the debugger.",
                        CommandOptionType.NoValue);
                    var daemonOption = x.Option(
                        "--daemon",
                        "Run the command over and over, forever.",
                        CommandOptionType.NoValue);
                    var successSleepOption = x.Option<ushort>(
                        "--success-sleep",
                        "The number of seconds to sleep when the command executed successfully and running as a daemon. Defaults to 1 second.",
                        CommandOptionType.SingleValue);
                    var failureSleepOption = x.Option<ushort>(
                        "--failure-sleep",
                        "The number of seconds to sleep when the command failed and running as a daemon. Defaults to 30 seconds.",
                        CommandOptionType.SingleValue);

                    command.Configure(x);

                    x.OnExecuteAsync(async cancellationToken =>
                    {
                        if (debugOption.HasValue())
                        {
                            Debugger.Launch();
                        }

                        var successSleepDuration = TimeSpan.FromSeconds(successSleepOption.HasValue() ? successSleepOption.ParsedValue : 1);
                        var failureSleepDuration = TimeSpan.FromSeconds(failureSleepOption.HasValue() ? failureSleepOption.ParsedValue : 30);

                        bool success;
                        do
                        {
                            token.ThrowIfCancellationRequested();

                            var commandRunner = new CommandExecutor(
                                   command,
                                   serviceProvider.GetRequiredService<ILogger<CommandExecutor>>());

                            success = await commandRunner.ExecuteAsync(token);

                            if (daemonOption.HasValue())
                            {
                                if (success)
                                {
                                    logger.LogInformation(
                                        "Waiting for {SuccessSleepDurationMs}ms since the command completed successfully." + Environment.NewLine,
                                        successSleepDuration.TotalMilliseconds);
                                    await Task.Delay(successSleepDuration);
                                }
                                else
                                {
                                    logger.LogInformation("Waiting for {FailureSleepDurationMs}ms since the command failed." + Environment.NewLine,
                                        failureSleepDuration.TotalMilliseconds);
                                    await Task.Delay(failureSleepDuration);
                                }
                            }
                        }
                        while (daemonOption.HasValue());

                        return success ? 0 : 1;
                    });
                });
        }

        public static ServiceCollection InitializeServiceCollection()
        {
            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);

            var configuration = configurationBuilder.Build();

            var serviceCollection = new ServiceCollection();

            serviceCollection.Configure<NuGetInsightsSettings>(configuration.GetSection(NuGetInsightsSettings.DefaultSectionName));
            serviceCollection.Configure<NuGetInsightsWorkerSettings>(configuration.GetSection(NuGetInsightsSettings.DefaultSectionName));

            serviceCollection.AddSingleton<TelemetryClient>();

            serviceCollection.AddNuGetInsights(configuration, "NuGet.Insights.Tool");
            serviceCollection.AddNuGetInsightsWorker();
            serviceCollection.AddNuGetInsightsWebsite();

            serviceCollection.AddLogging(o =>
            {
                o.SetMinimumLevel(LogLevel.Trace);
                o.AddMinimalConsole();
            });

            foreach (var pair in Commands)
            {
                serviceCollection.AddSingleton(pair.Value);
            }

            return serviceCollection;
        }
    }
}
