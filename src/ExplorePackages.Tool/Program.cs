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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Tool
{
    public class Program
    {
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
            var logger = new LoggerFactory().AddMinimalConsole().CreateLogger<Program>();

            // Read and show the settings
            logger.LogInformation("===== settings =====");
            var settings = ReadSettingsFromDisk(logger) ?? new ExplorePackagesSettings();
            logger.LogInformation(JsonConvert.SerializeObject(settings, Formatting.Indented));
            logger.LogInformation("====================" + Environment.NewLine);

            // Allow 32 concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 32;

            // Initialize the dependency injection container.
            var serviceCollection = InitializeServiceCollection(settings);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                logger = serviceProvider.GetRequiredService<ILogger<Program>>();

                var app = new CommandLineApplication();
                app.HelpOption();

                foreach (var pair in Commands)
                {
                    AddCommand(pair.Value, serviceProvider, app, pair.Key, logger);
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
            var settings = JsonConvert.DeserializeObject<ExplorePackagesSettings>(content);

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
                            serviceProvider.GetRequiredService<ExplorePackagesSettings>(),
                            command.IsDatabaseRequired(),
                            serviceProvider.GetRequiredService<ILogger<Program>>());

                        do
                        {
                            var commandRunner = new CommandExecutor(
                                command,
                                serviceProvider.GetRequiredService<ILogger<CommandExecutor>>());

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
        
        private static async Task InitializeGlobalState(ExplorePackagesSettings settings, bool initializeDatabase, ILogger logger)
        {
            logger.LogInformation("===== initialize =====");

            // Initialize the database.
            EntityContext.ConnectionString = "Data Source=" + settings.DatabasePath;
            EntityContext.Enabled = initializeDatabase;
            if (initializeDatabase)
            {
                using (var entityContext = new EntityContext())
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

            logger.LogInformation("======================" + Environment.NewLine);
        }

        private static ServiceCollection InitializeServiceCollection(ExplorePackagesSettings settings)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddExplorePackages(settings);
            
            serviceCollection.AddSingleton(new LoggerFactory().AddMinimalConsole());
            serviceCollection.AddLogging();

            foreach (var pair in Commands)
            {
                serviceCollection.AddTransient(pair.Value);
            }

            return serviceCollection;
        }
    }
}
