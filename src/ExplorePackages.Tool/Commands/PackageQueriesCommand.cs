using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public class PackageQueriesCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly PackageQueryExecutor _executor;
        private readonly PackageQueryCollector _processor;
        private readonly ProblemService _problemService;
        private readonly PackageQueryFactory _packageQueryFactory;
        private readonly ILogger<PackageQueriesCommand> _logger;

        private CommandOption _reprocessOption;
        private CommandOption _resumeOption;
        private CommandOption _queriesOption;
        private CommandOption _idsOption;
        private CommandOption _versionsOption;
        private CommandOption _problemQueries;

        public PackageQueriesCommand(
            CursorService cursorService,
            PackageQueryExecutor executor,
            PackageQueryCollector processor,
            ProblemService problemService,
            PackageQueryFactory packageQueryFactory,
            ILogger<PackageQueriesCommand> logger)
        {
            _cursorService = cursorService;
            _executor = executor;
            _processor = processor;
            _problemService = problemService;
            _packageQueryFactory = packageQueryFactory;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            const string idTemplate = "--id";
            const string versionTemplate = "--version";

            _reprocessOption = app.Option(
                "--reprocess",
                "Reprocess package query results.",
                CommandOptionType.NoValue);
            _resumeOption = app.Option(
                "--resume",
                "Resume the current reprocess operation.",
                CommandOptionType.NoValue);
            _queriesOption = app.Option(
                "--query",
                "The query or queries to process.",
                CommandOptionType.MultipleValue);
            _idsOption = app.Option(
                idTemplate,
                $"The IDs of the specific packages to process. This must have the same number of values specified as the {versionTemplate} option.",
                CommandOptionType.MultipleValue);
            _versionsOption = app.Option(
                "--version",
                $"The versions of the specific package to process. This must have the same number of values specified as the {idTemplate} option.",
                CommandOptionType.MultipleValue);
            _problemQueries = app.Option(
                "--problem-queries",
                $"Process only queries related to the {nameof(ProblemService)}.",
                CommandOptionType.NoValue);
        }

        private bool Reprocess => _reprocessOption?.HasValue() ?? false;
        private bool Resume => _resumeOption?.HasValue() ?? false;
        private IReadOnlyList<string> QueryNames => _queriesOption?.Values ?? new List<string>();
        private IReadOnlyList<string> Ids => _idsOption?.Values ?? new List<string>();
        private IReadOnlyList<string> Versions => _versionsOption?.Values ?? new List<string>();
        private bool ProblemQueries => _problemQueries?.HasValue() ?? false;

        public async Task ExecuteAsync(CancellationToken token)
        {
            if (Reprocess && !Resume)
            {
                await _cursorService.ResetValueAsync(CursorNames.ReprocessPackageQueries);
            }

            var allQueries = _packageQueryFactory.Get();
            var queries = allQueries;
            if (ProblemQueries)
            {
                queries = queries
                    .Where(x => _problemService.ProblemQueryNames.Contains(x.Name))
                    .ToList();
            }
            else if (QueryNames.Any())
            {
                queries = queries
                    .Where(x => QueryNames.Contains(x.Name))
                    .ToList();
            }

            if (queries.Count != allQueries.Count)
            {
                var queryNames = string.Join(
                    Environment.NewLine,
                    queries.Select(x => x.Name).OrderBy(x => x).Select(x => $" - {x}"));
                _logger.LogInformation("Executing the following package queries:" + Environment.NewLine + queryNames);
            }
            else
            {
                _logger.LogInformation("Executing all package queries.");
            }

            if (Ids.Any() || Versions.Any())
            {
                if (Ids.Count != Versions.Count)
                {
                    _logger.LogError(
                        $"There are {{IdsCount}} {_idsOption.LongName} values specified but {{VersionsCount}} " +
                        $"{_versionsOption.LongName} values specified. There must be the same number.",
                        Ids.Count,
                        Versions.Count);
                    return;
                }

                var identities = Ids
                    .Zip(Versions, (id, version) => new PackageIdentity(id.Trim(), version.Trim()))
                    .ToList();

                await _executor.ProcessPackageAsync(queries, identities);
            }
            else
            {
                await _processor.ProcessAsync(queries, Reprocess);
            }
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
