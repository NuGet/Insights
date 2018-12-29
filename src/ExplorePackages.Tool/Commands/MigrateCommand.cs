using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class MigrateCommand : ICommand
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MigrateCommand> _logger;
        private CommandOption<DatabaseType> _typeOption;
        private CommandOption<string> _connectionOption;

        public MigrateCommand(IServiceProvider serviceProvider, ILogger<MigrateCommand> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _typeOption = app
                .Option<DatabaseType>(
                    "--type",
                    "The database type.",
                    CommandOptionType.SingleValue);
            _connectionOption = app
                .Option<string>(
                    "--connection",
                    "The connection string used to connect to the database.",
                    CommandOptionType.SingleValue);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            using (var migrationScope = _serviceProvider.CreateScope())
            {
                var options = migrationScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>();
                if (_typeOption != null && _typeOption.HasValue())
                {
                    options.Value.DatabaseType = _typeOption.ParsedValue;
                }

                if (_connectionOption != null && _connectionOption.HasValue())
                {
                    options.Value.DatabaseConnectionString = _connectionOption.ParsedValue;
                }

                using (var loggingScope = migrationScope.ServiceProvider.CreateScope())
                {
                    loggingScope.ServiceProvider.SanitizeAndLogSettings();
                }   

                using (var entityContext = migrationScope.ServiceProvider.GetRequiredService<IEntityContext>())
                {
                    _logger.LogInformation("Getting applied migrations...");
                    var appliedMigrations = (await entityContext.Database.GetAppliedMigrationsAsync()).ToList();
                    _logger.LogInformation(
                        "Found {Count} applied migrations." + Environment.NewLine + "{Names}",
                        appliedMigrations.Count,
                        appliedMigrations);

                    _logger.LogInformation("Getting pending migrations...");
                    var pendingMigrations = (await entityContext.Database.GetPendingMigrationsAsync()).ToList();
                    _logger.LogInformation(
                        "Found {Count} pending migrations." + Environment.NewLine + "{Names}",
                        pendingMigrations.Count,
                        pendingMigrations);

                    if (pendingMigrations.Any())
                    {
                        _logger.LogInformation("Applying migrations...");
                        await entityContext.Database.MigrateAsync();
                    }
                }
            }
        }

        public bool IsInitializationRequired() => false;
        public bool IsDatabaseRequired() => false;
        public bool IsReadOnly() => true;
    }
}
