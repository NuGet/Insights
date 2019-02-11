using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Tool.Commands;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Tool
{
    public class Program
    {
        private static readonly TimeSpan ReleaseInDuration = TimeSpan.FromSeconds(15);

        private static IReadOnlyDictionary<string, Type> Commands = new Dictionary<string, Type>
        {
            { "backup-database", typeof(BackupDatabaseCommand) },
            { "break-singleton-lease", typeof(BreakSingletonLeaseCommand) },
            { "catalog-to-database", typeof(CatalogToDatabaseCommand) },
            { "catalog-to-nuspecs", typeof(CatalogToNuspecsCommand) },
            { "check-package", typeof(CheckPackageCommand) },
            { "dependencies-to-database", typeof(DependenciesToDatabaseCommand) },
            { "dependency-packages-to-database", typeof(DependencyPackagesToDatabaseCommand) },
            { "downloads-to-database", typeof(DownloadsToDatabaseCommand) },
            { "fetch-cursors", typeof(FetchCursorsCommand) },
            { "migrate", typeof(MigrateCommand) },
            { "mzip", typeof(MZipCommand) },
            { "mzip-to-database", typeof(MZipToDatabaseCommand) },
            { "package-queries", typeof(PackageQueriesCommand) },
            { "reprocess-cross-check-discrepancies", typeof(ReprocessCrossCheckDiscrepanciesCommand) },
            { "reset-cursor", typeof(ResetCursorCommand) },
            { "sandbox", typeof(SandboxCommand) },
            { "show-problems", typeof(ShowProblemsCommand) },
            { "show-query-results", typeof(ShowQueryResultsCommand) },
            { "show-repositories", typeof(ShowRepositoriesCommand) },
            { "show-weird-dependencies", typeof(ShowWeirdDependenciesCommand) },
            { "show-weird-metadata", typeof(ShowWeirdMetadataCommand) },
            { "update", typeof(UpdateCommand) },
            { "v2-to-database", typeof(V2ToDatabaseCommand) },
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
            var singletonService = serviceProvider.GetRequiredService<ISingletonService>();
            await cancelEvent.WaitAsync();
            logger.LogWarning("Cancelling...");

            try
            {
                await singletonService.ReleaseInAsync(ReleaseInDuration);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected exception occured while cancelling.");
            }

            return 1;
        }

        private static async Task<int> ExecuteAsync(
            string[] args,
            IServiceProvider serviceProvider,
            CancellationToken token)
        {
            await Task.Yield();

            var app = new CommandLineApplication();
            app.HelpOption();
            app.OnExecute(() => app.ShowHelp());

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var singletonService = serviceProvider.GetRequiredService<ISingletonService>();

            foreach (var pair in Commands)
            {
                AddCommand(pair.Value, serviceProvider, app, pair.Key, logger, token);
            }

            try
            {
                var output = app.Execute(args);
                await singletonService.ReleaseInAsync(output == 0 ? TimeSpan.Zero : ReleaseInDuration);
                return output;
            }
            catch (Exception ex)
            {
                await singletonService.ReleaseInAsync(ReleaseInDuration);
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
            var singletonService = serviceProvider.GetRequiredService<ISingletonService>();
            var batchSizeProvider = serviceProvider.GetRequiredService<IBatchSizeProvider>();

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
                    var batchSizeOption = x.Option<int>(
                        "--batch-size",
                        "The batch size to use. This overrides the default batch size specific to each operation.",
                        CommandOptionType.SingleValue);

                    command.Configure(x);

                    x.OnExecute(async () =>
                    {
                        if (debugOption.HasValue())
                        {
                            Debugger.Launch();
                        }

                        var successSleepDuration = TimeSpan.FromSeconds(successSleepOption.HasValue() ? successSleepOption.ParsedValue : 1);
                        var failureSleepDuration = TimeSpan.FromSeconds(failureSleepOption.HasValue() ? failureSleepOption.ParsedValue : 30);

                        if (command.IsInitializationRequired())
                        {
                            await InitializeGlobalState(
                                serviceProvider,
                                command.IsDatabaseRequired());
                        }

                        if (batchSizeOption.HasValue())
                        {
                            batchSizeProvider.Set(batchSizeOption.ParsedValue);
                        }

                        bool success;
                        do
                        {
                            token.ThrowIfCancellationRequested();

                            var commandRunner = new CommandExecutor(
                                   command,
                                   singletonService,
                                   serviceProvider.GetRequiredService<ILogger<CommandExecutor>>());

                            success = await commandRunner.ExecuteAsync(token);

                            if (daemonOption.HasValue())
                            {
                                if (success)
                                {
                                    batchSizeProvider.Increase();
                                    logger.LogInformation(
                                        "Waiting for {SuccessSleepDuration} since the command completed successfully." + Environment.NewLine,
                                        successSleepDuration);
                                    await Task.Delay(successSleepDuration);
                                }
                                else
                                {
                                    batchSizeProvider.Decrease();
                                    logger.LogInformation("Waiting for {FailureSleepDuration} since the command failed." + Environment.NewLine,
                                        failureSleepDuration);
                                    await Task.Delay(failureSleepDuration);
                                }
                            }
                        }
                        while (daemonOption.HasValue());

                        return success ? 0 : 1;
                    });
                });
        }
        
        private static async Task InitializeGlobalState(
            IServiceProvider serviceProvider,
            bool initializeDatabase)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("===== initialize =====");

            // Log the settings
            using (var loggingScope = serviceProvider.CreateScope())
            {
                loggingScope.ServiceProvider.SanitizeAndLogSettings();
            }

            // Initialize the database.
            if (initializeDatabase)
            {
                using (var entityContext = serviceProvider.GetRequiredService<IEntityContext>())
                {
                    await entityContext.Database.MigrateAsync();
                    logger.LogInformation("The database schema is up to date.");
                }
            }
            else
            {
                logger.LogInformation("The database will not be used.");
            }

            // Set the user agent.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages.Bot");
            UserAgent.SetUserAgentString(userAgentStringBuilder);
            logger.LogInformation("The following user agent will be used: {UserAgent}", UserAgent.UserAgentString);

            // Allow 32 concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 64;

            logger.LogInformation("======================" + Environment.NewLine);
        }

        private static ServiceCollection InitializeServiceCollection()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddExplorePackages();

            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Directory.GetCurrentDirectory();
            var userProfilePath = Path.Combine(userProfile, "Knapcode.ExplorePackages.Settings.json");

            var local = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            var localPath = Path.Combine(local, "Knapcode.ExplorePackages.Settings.json");

            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(userProfilePath, optional: true, reloadOnChange: false)
                .AddJsonFile(localPath, optional: true, reloadOnChange: false);
            var configuration = configurationBuilder.Build();

            serviceCollection.Configure<ExplorePackagesSettings>(configuration.GetSection("Knapcode.ExplorePackages"));

            serviceCollection.AddLogging(o =>
            {
                o.SetMinimumLevel(LogLevel.Trace);
                o.AddFilter(DbLoggerCategory.Name, LogLevel.Warning);
                o.AddMinimalConsole();
            });

            foreach (var pair in Commands)
            {
                serviceCollection.AddTransient(pair.Value);
            }

            return serviceCollection;
        }
    }
}
