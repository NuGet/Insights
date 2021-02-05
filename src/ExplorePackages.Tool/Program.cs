using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public class Program
    {
        private static readonly IReadOnlyDictionary<string, Type> Commands = new Dictionary<string, Type>
        {
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
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
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
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddExplorePackages("Knapcode.ExplorePackages.Tool");
            serviceCollection.AddExplorePackagesWorker();
            AddExplorePackagesSettings<Program>(serviceCollection);

            serviceCollection.AddLogging(o =>
            {
                o.SetMinimumLevel(LogLevel.Trace);
                o.AddMinimalConsole();
            });

            foreach (var pair in Commands)
            {
                serviceCollection.AddTransient(pair.Value);
            }

            return serviceCollection;
        }

        private static IServiceCollection AddExplorePackagesSettings<T>(IServiceCollection serviceCollection)
        {
            var localDirectory = Path.GetDirectoryName(typeof(T).Assembly.Location);
            return AddExplorePackagesSettings(serviceCollection, localDirectory);
        }

        private static IServiceCollection AddExplorePackagesSettings(
            IServiceCollection serviceCollection,
            string localDirectory = null)
        {
            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Directory.GetCurrentDirectory();
            var userProfilePath = Path.Combine(userProfile, "Knapcode.ExplorePackages.Settings.json");

            var localPath = Path.Combine(
                localDirectory ?? typeof(ServiceCollectionExtensions).Assembly.Location,
                "Knapcode.ExplorePackages.Settings.json");

            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(userProfilePath, optional: true, reloadOnChange: false)
                .AddJsonFile(localPath, optional: true, reloadOnChange: false);

            var configuration = configurationBuilder.Build();

            serviceCollection.Configure<ExplorePackagesSettings>(configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));
            serviceCollection.Configure<ExplorePackagesWorkerSettings>(configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));

            return serviceCollection;
        }
    }
}
