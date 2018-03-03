using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Commands;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Support;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NuGet.Common;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages
{
    public class Program
    {
        private static IReadOnlyDictionary<string, Type> Commands = new Dictionary<string, Type>
        {
            { "catalogtodatabase", typeof(CatalogToDatabaseCommand) },
            { "catalogtonuspecs", typeof(CatalogToNuspecsCommand) },
            { "checkpackage", typeof(CheckPackageCommand) },
            { "downloadstodatabase", typeof(DownloadsToDatabaseCommand) },
            { "fetchcursors", typeof(FetchCursorsCommand) },
            { "mzip", typeof(MZipCommand) },
            { "mziptodatabase", typeof(MZipToDatabaseCommand) },
            { "packagequeries", typeof(PackageQueriesCommand) },
            { "sandbox", typeof(SandboxCommand) },
            { "showqueryresults", typeof(ShowQueryResultsCommand) },
            { "showrepositories", typeof(ShowRepositoriesCommand) },
            { "showweirddependencies", typeof(ShowWeirdDependenciesCommand) },
            { "update", typeof(UpdateCommand) },
            { "v2todatabase", typeof(V2ToDatabaseCommand) },
        };

        public static void Main(string[] args)
        {
            // Read and show the settings
            Console.WriteLine("===== settings =====");
            var settings = ReadSettingsFromDisk() ?? new ExplorePackagesSettings();
            Console.WriteLine(JsonConvert.SerializeObject(settings, Formatting.Indented));
            Console.WriteLine("====================");
            Console.WriteLine();

            // Allow 32 concurrent outgoing connections.
            ServicePointManager.DefaultConnectionLimit = 32;

            // Initialize the dependency injection container.
            var serviceCollection = InitializeServiceCollection(settings);
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var app = new CommandLineApplication();
                app.HelpOption();

                foreach (var pair in Commands)
                {
                    AddCommand(pair.Value, serviceProvider, app, pair.Key);
                }

                app.Execute(args);
            }
        }

        private static void AddCommand(
            Type commandType,
            IServiceProvider serviceProvider,
            CommandLineApplication app,
            string commandName)
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

                    command.Configure(x);

                    x.OnExecute(async () =>
                    {
                        if (debugOption.HasValue())
                        {
                            Debugger.Launch();
                        }

                        await InitializeGlobalState(
                            serviceProvider.GetRequiredService<ExplorePackagesSettings>(),
                            command.IsDatabaseRequired(),
                            serviceProvider.GetRequiredService<ILogger>());

                        var commandRunner = new CommandExecutor(
                            command,
                            serviceProvider.GetRequiredService<ILogger>());

                        await commandRunner.ExecuteAsync(CancellationToken.None);

                        return 0;
                    });
                });
        }
        
        private static async Task InitializeGlobalState(ExplorePackagesSettings settings, bool initializeDatabase, ILogger log)
        {
            Console.WriteLine("===== initialize =====");

            // Initialize the database.
            EntityContext.ConnectionString = "Data Source=" + settings.DatabasePath;
            EntityContext.Enabled = initializeDatabase;
            if (initializeDatabase)
            {
                using (var entityContext = new EntityContext())
                {
                    await entityContext.Database.MigrateAsync();
                    log.LogInformation("The database schema is up to date.");
                }
            }
            else
            {
                log.LogInformation("The database will not be used.");
            }

            // Set the user agent.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages.Bot");
            UserAgent.SetUserAgentString(userAgentStringBuilder);
            log.LogInformation($"The following user agent will be used: {UserAgent.UserAgentString}");

            Console.WriteLine("======================");
            Console.WriteLine();
        }

        private static ExplorePackagesSettings ReadSettingsFromDisk()
        {
            var settingsDirectory = Environment.GetEnvironmentVariable("USERPROFILE") ?? Directory.GetCurrentDirectory();
            var settingsPath = Path.Combine(settingsDirectory, "Knapcode.ExplorePackages.Settings.json");
            if (!File.Exists(settingsPath))
            {
                Console.WriteLine($"No settings existed at {settingsPath}.");
                return null;
            }

            Console.WriteLine($"Settings will be read from {settingsPath}.");
            var content = File.ReadAllText(settingsPath);
            var settings = JsonConvert.DeserializeObject<ExplorePackagesSettings>(content);

            return settings;
        }

        private static ServiceCollection InitializeServiceCollection(ExplorePackagesSettings settings)
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddExplorePackages(settings);

            serviceCollection.AddSingleton<ILogger, ConsoleLogger>();

            foreach (var pair in Commands)
            {
                serviceCollection.AddTransient(pair.Value);
            }

            return serviceCollection;
        }
    }
}
