using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class BackupDatabaseCommand : ICommand
    {
        private readonly ILogger<BackupDatabaseCommand> _logger;
        private CommandOption _destinationOption;

        public BackupDatabaseCommand(ILogger<BackupDatabaseCommand> logger)
        {
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
            using (var entityContext = new EntityContext())
            {
                var destination = _destinationOption.Value();
                _logger.LogInformation("Backing up the database to: {destination}", _destinationOption.Value());
                var stopwatch = Stopwatch.StartNew();
                await entityContext.BackupDatabaseAsync(destination);
                _logger.LogInformation("Done. Took {duration}.", stopwatch.Elapsed);
            }
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
