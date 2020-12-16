using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Tool
{
    public class UpdateCommand : ICommand
    {
        private readonly IReadOnlyList<ICommand> _commands;
        private readonly ISingletonService _singletonService;
        private readonly ILogger<CommandExecutor> _logger;
        private CommandOption _skipDownloadsOption;

        public UpdateCommand(
            V2ToDatabaseCommand v2ToDatabase,
            FetchCursorsCommand fetchCursors,
            CatalogToDatabaseCommand catalogToDatabase,
            NuspecsCommand nuspecs,
            MZipsCommand mzips,
            MZipToDatabaseCommand mzipToDatabase,
            DependenciesToDatabaseCommand dependenciesToDatabase,
            DependencyPackagesToDatabaseCommand dependencyPackagesToDatabase,
            DownloadsToDatabaseCommand downloadsToDatabase,
            PackageQueriesCommand packageQueries,
            ISingletonService singletonService,
            IOptions<ExplorePackagesSettings> options,
            ILogger<CommandExecutor> logger)
        {
            var commands = new List<ICommand>
            {
                v2ToDatabase,
                fetchCursors,
                catalogToDatabase,
                nuspecs,
                mzips,
                mzipToDatabase,
                dependenciesToDatabase,
                dependencyPackagesToDatabase,
            };

            if (options.Value.DownloadsV1Url != null)
            {
                commands.Add(downloadsToDatabase);
            }

            commands.AddRange(new ICommand[]
            {
                packageQueries,
            });

            _commands = commands;
            _singletonService = singletonService;
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
            var success = true;

            foreach (var command in _commands)
            {
                token.ThrowIfCancellationRequested();

                if (command is DownloadsToDatabaseCommand && SkipDownloads)
                {
                    continue;
                }

                var commandExecutor = new CommandExecutor(command, _singletonService, _logger);
                success &= await commandExecutor.ExecuteAsync(token);
            }

            if (!success)
            {
                throw new InvalidOperationException("One or more of the update steps failed.");
            }
        }

        public bool IsInitializationRequired() => _commands.Any(x => x.IsInitializationRequired());
        public bool IsDatabaseRequired() => _commands.Any(x => x.IsDatabaseRequired());
        public bool IsSingleton() => _commands.Any(x => x.IsSingleton());
    }
}
