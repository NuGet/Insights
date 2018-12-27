using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Tool.Commands;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Tool
{
    public class Program
    {
        private static readonly TimeSpan ReleaseInDuration = TimeSpan.FromSeconds(15);
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Converters =
            {
                new StringEnumConverter(),
            },
            Formatting = Formatting.Indented,
        };

        private static IReadOnlyDictionary<string, Type> Commands = new Dictionary<string, Type>
        {
            { "backupdatabase", typeof(BackupDatabaseCommand) },
            { "catalogtodatabase", typeof(CatalogToDatabaseCommand) },
            { "catalogtonuspecs", typeof(CatalogToNuspecsCommand) },
            { "checkpackage", typeof(CheckPackageCommand) },
            { "dependenciestodatabase", typeof(DependenciesToDatabaseCommand) },
            { "dependencypackagestodatabase", typeof(DependencyPackagesToDatabaseCommand) },
            { "downloadstodatabase", typeof(DownloadsToDatabaseCommand) },
            { "fetchcursors", typeof(FetchCursorsCommand) },
            { "mzip", typeof(MZipCommand) },
            { "mziptodatabase", typeof(MZipToDatabaseCommand) },
            { "packagequeries", typeof(PackageQueriesCommand) },
            { "reprocesscrosscheckdiscrepancies", typeof(ReprocessCrossCheckDiscrepanciesCommand) },
            { "sandbox", typeof(SandboxCommand) },
            { "showproblems", typeof(ShowProblemsCommand) },
            { "showqueryresults", typeof(ShowQueryResultsCommand) },
            { "showrepositories", typeof(ShowRepositoriesCommand) },
            { "showweirddependencies", typeof(ShowWeirdDependenciesCommand) },
            { "showweirdmetadata", typeof(ShowWeirdMetadataCommand) },
            { "update", typeof(UpdateCommand) },
            { "v2todatabase", typeof(V2ToDatabaseCommand) },
        };

        public static int Main(string[] args)
        {
            return MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task<int> MainAsync(string[] args)
        {
            // Initialize a logger just for bootstrapping.
            var logger = new LoggerFactory().AddMinimalConsole().CreateLogger<Program>();

            // Read and show the settings
            logger.LogInformation("===== settings =====");
            var settings = ReadSettingsFromDisk(logger) ?? new ExplorePackagesSettings();
            LogSettings(logger, settings);
            logger.LogInformation("====================" + Environment.NewLine);

            // Initialize the dependency injection container.
            var serviceCollection = InitializeServiceCollection(settings);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                // Set up the cancel event to release the lease if someone hits Ctrl + C while the program is running.
                var cancelEvent = new SemaphoreSlim(0);
                Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancelEvent.Release();
                };

                // Wait for cancellation and execute the program in parallel.
                var cancelTask = WaitForCancellationAsync(cancelEvent, serviceProvider);
                var executeTask = ExecuteAsync(args, serviceProvider);

                return await await Task.WhenAny(cancelTask, executeTask);
            }
        }

        private static void LogSettings(ILogger<Program> logger, ExplorePackagesSettings settings)
        {
            var sanitized = settings.Clone();

            // Sanitize the DB connection string
            sanitized.DatabaseConnectionString = Regex.Replace(
                sanitized.DatabaseConnectionString,
                "(User ID|UID|Password|PWD)=[^;]*",
                "$1=(redacted)",
                RegexOptions.IgnoreCase);

            // Sanitize the Azure Blob Storage connection string
            sanitized.StorageConnectionString = Regex.Replace(
                sanitized.StorageConnectionString,
                "(SharedAccessSignature|AccountKey)=[^;]*",
                "$1=(redacted)", 
                RegexOptions.IgnoreCase);

            logger.LogInformation(JsonConvert.SerializeObject(sanitized, SerializerSettings));
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
            IServiceProvider serviceProvider)
        {
            await Task.Yield();

            var app = new CommandLineApplication();
            app.HelpOption();
            app.OnExecute(() => app.ShowHelp());

            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var singletonService = serviceProvider.GetRequiredService<ISingletonService>();

            foreach (var pair in Commands)
            {
                AddCommand(pair.Value, serviceProvider, app, pair.Key, logger);
            }

            try
            {
                var output = app.Execute(args);
                await singletonService.ReleaseInAsync(TimeSpan.Zero);
                return output;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected exception occured.");
                return 1;
            }
            finally
            {
                await singletonService.ReleaseInAsync(ReleaseInDuration);
            }
        }

        private static ExplorePackagesSettings ReadSettingsFromDisk(ILogger logger)
        {
            var settingsDirectory = Environment.GetEnvironmentVariable("USERPROFILE") ?? Directory.GetCurrentDirectory();
            var settingsPath = Path.Combine(settingsDirectory, "Knapcode.ExplorePackages.Settings.json");
            if (!File.Exists(settingsPath))
            {
                logger.LogInformation("No settings existed at {SettingsPath}.", settingsPath);
                return null;
            }

            logger.LogInformation("Settings will be read from {SettingsPath}.", settingsPath);
            var content = File.ReadAllText(settingsPath);
            var settings = JsonConvert.DeserializeObject<ExplorePackagesSettings>(content, SerializerSettings);

            return settings;
        }

        private static void AddCommand(
            Type commandType,
            IServiceProvider serviceProvider,
            CommandLineApplication app,
            string commandName,
            ILogger logger)
        {
            var command = (ICommand)serviceProvider.GetRequiredService(commandType);
            var singletonService = serviceProvider.GetRequiredService<ISingletonService>();

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

                    x.OnExecute(async () =>
                    {
                        if (debugOption.HasValue())
                        {
                            Debugger.Launch();
                        }

                        var successSleepDuration = TimeSpan.FromSeconds(successSleepOption.HasValue() ? successSleepOption.ParsedValue : 1);
                        var failureSleepDuration = TimeSpan.FromSeconds(failureSleepOption.HasValue() ? failureSleepOption.ParsedValue : 30);

                        await InitializeGlobalState(
                            serviceProvider,
                            command.IsDatabaseRequired(),
                            !command.IsReadOnly(),
                            serviceProvider.GetRequiredService<ExplorePackagesSettings>(),
                            serviceProvider.GetRequiredService<ILogger<Program>>());

                        do
                        {
                            var commandRunner = new CommandExecutor(
                                command,
                                serviceProvider.GetRequiredService<ILogger<CommandExecutor>>());

                            if (!command.IsReadOnly())
                            {
                                await singletonService.RenewAsync();
                            }

                            var success = await commandRunner.ExecuteAsync(CancellationToken.None);

                            if (daemonOption.HasValue())
                            {
                                if (success)
                                {
                                    logger.LogInformation(
                                        "Waiting for {SuccessSleepDuration} since the command completed successfully." + Environment.NewLine,
                                        successSleepDuration);
                                    await Task.Delay(successSleepDuration);
                                }
                                else
                                {
                                    logger.LogInformation("Waiting for {FailureSleepDuration} since the command failed." + Environment.NewLine,
                                        failureSleepDuration);
                                    await Task.Delay(failureSleepDuration);
                                }
                            }
                            
                        }
                        while (daemonOption.HasValue());

                        return 0;
                    });
                });
        }
        
        private static async Task InitializeGlobalState(
            IServiceProvider serviceProvider,
            bool initializeDatabase,
            bool acquireSingletonLease,
            ExplorePackagesSettings settings,
            ILogger logger)
        {
            logger.LogInformation("===== initialize =====");

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

            // Acquire the singleton lease.
            if (acquireSingletonLease)
            {
                logger.LogInformation("Ensuring that this job is a singleton.");
                var singletonService = serviceProvider.GetRequiredService<ISingletonService>();
                await singletonService.AcquireOrRenewAsync();
            }

            // Set the user agent.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages.Bot");
            UserAgent.SetUserAgentString(userAgentStringBuilder);
            logger.LogInformation("The following user agent will be used: {UserAgent}", UserAgent.UserAgentString);

            // Allow 32 concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 32;

            logger.LogInformation("======================" + Environment.NewLine);
        }

        private static ServiceCollection InitializeServiceCollection(ExplorePackagesSettings settings)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddExplorePackages(settings);

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
