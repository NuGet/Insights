using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class UpdateCommand : ICommand
    {
        private readonly IReadOnlyList<ICommand> _commands;
        private readonly ILogger _log;
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
            ILogger log)
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
            _log = log;
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

                var commandExecutor = new CommandExecutor(command, _log);
                await commandExecutor.ExecuteAsync(token);
            }
        }

        public bool IsDatabaseRequired()
        {
            return _commands.Any(x => x.IsDatabaseRequired());
        }
    }
}
