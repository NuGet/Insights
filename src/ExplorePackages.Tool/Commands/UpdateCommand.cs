using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class UpdateCommand : ICommand
    {
        private readonly IReadOnlyList<ICommand> _commands;
        private readonly ILogger<CommandExecutor> _logger;
        private CommandOption _skipDownloadsOption;

        public UpdateCommand(
            V2ToDatabaseCommand v2ToDatabase,
            FetchCursorsCommand fetchCursors,
            CatalogToDatabaseCommand catalogToDatabase,
            CatalogToNuspecsCommand catalogToNuspecs,
            MZipCommand mzip,
            MZipToDatabaseCommand mzipToDatabase,
            DependenciesToDatabaseCommand dependenciesToDatabase,
            DependencyPackagesToDatabaseCommand dependencyPackagesToDatabase,
            DownloadsToDatabaseCommand downloadsToDatabase,
            PackageQueriesCommand packageQueries,
            ExplorePackagesSettings settings,
            ILogger<CommandExecutor> logger)
        {
            var commands = new List<ICommand>
            {
                v2ToDatabase,
                fetchCursors,
                catalogToDatabase,
                catalogToNuspecs,
                mzip,
                mzipToDatabase,
                dependenciesToDatabase,
                dependencyPackagesToDatabase,
            };

            if (settings.DownloadsV1Url != null)
            {
                commands.Add(downloadsToDatabase);
            }

            commands.AddRange(new[]
            {
                packageQueries,
            });

            _commands = commands;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _skipDownloadsOption = app.Option(
                "--skip-downloads",
                "Skip the downloadstodatabase command.",
                CommandOptionType.NoValue);
        }

        private bool SkipDownloads => _skipDownloadsOption?.HasValue() ?? false;

        public async Task ExecuteAsync(CancellationToken token)
        {
            foreach (var command in _commands)
            {
                if (command is DownloadsToDatabaseCommand && SkipDownloads)
                {
                    continue;
                }

                var commandExecutor = new CommandExecutor(command, _logger);
                await commandExecutor.ExecuteAsync(token);
            }
        }

        public bool IsDatabaseRequired()
        {
            return _commands.Any(x => x.IsDatabaseRequired());
        }
    }
}
