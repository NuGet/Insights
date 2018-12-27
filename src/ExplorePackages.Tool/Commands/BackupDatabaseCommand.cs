using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class BackupDatabaseCommand : ICommand
    {
        private readonly EntityContextFactory _entityContextFactory;
        private readonly ILogger<BackupDatabaseCommand> _logger;
        private CommandOption _destinationOption;

        public BackupDatabaseCommand(
            EntityContextFactory entityContextFactory,
            ILogger<BackupDatabaseCommand> logger)
        {
            _entityContextFactory = entityContextFactory;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _destinationOption = app.Option(
                "--destination",
                "The file path of where the database will be backed up.",
                CommandOptionType.SingleValue,
                x => x.IsRequired());
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var sqliteEntityContext = entityContext as SqliteEntityContext;
                if (sqliteEntityContext == null)
                {
                    _logger.LogError("Backing up the database is only supported on SQLite databases.");
                    return;
                }

                var destination = _destinationOption.Value();
                _logger.LogInformation("Backing up the database to: {destination}", _destinationOption.Value());
                var stopwatch = Stopwatch.StartNew();
                await sqliteEntityContext.BackupDatabaseAsync(destination);
                _logger.LogInformation("Done. Took {duration}.", stopwatch.Elapsed);
            }
        }

        public bool IsDatabaseRequired() => true;
        public bool IsReadOnly() => true;
    }
}
