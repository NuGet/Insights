using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class PackageQueriesCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly PackageQueryProcessor _processor;
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

        private CommandOption _reprocessOption;
        private CommandOption _resumeOption;
        private CommandOption _queriesOption;
        private CommandOption _idsOption;
        private CommandOption _versionsOption;

        public PackageQueriesCommand(
            CursorService cursorService,
            PackageQueryProcessor processor,
            IEnumerable<IPackageQuery> queries,
            ILogger log)
        {
            _cursorService = cursorService;
            _processor = processor;
            _queries = queries.ToList();
            _log = log;
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
        }

        private bool Reprocess => _reprocessOption?.HasValue() ?? false;
        private bool Resume => _resumeOption?.HasValue() ?? false;
        private IReadOnlyList<string> QueryNames => _queriesOption?.Values ?? new List<string>();
        private IReadOnlyList<string> Ids => _idsOption?.Values ?? new List<string>();
        private IReadOnlyList<string> Versions => _versionsOption?.Values ?? new List<string>();

        public async Task ExecuteAsync(CancellationToken token)
        {
            if (Reprocess && !Resume)
            {
                await _cursorService.ResetValueAsync(CursorNames.ReprocessPackageQueries);
            }

            var queries = _queries.ToList();

            if (QueryNames.Any())
            {
                queries = queries
                    .Where(x => QueryNames.Contains(x.Name))
                    .ToList();
            }

            if (Ids.Any() || Versions.Any())
            {
                if (Ids.Count != Versions.Count)
                {
                    _log.LogError(
                        $"There are {Ids.Count} {_idsOption.Template} values specified but {Versions.Count} " +
                        $"{_versionsOption.Template} values specified. There must be the same number.");
                    return;
                }

                var identities = Ids
                    .Zip(Versions, (id, version) => new PackageIdentity(id.Trim(), version.Trim()))
                    .ToList();

                await _processor.ProcessPackageAsync(queries, identities, token);
            }
            else
            {
                await _processor.ProcessAsync(queries, Reprocess, token);
            }
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
