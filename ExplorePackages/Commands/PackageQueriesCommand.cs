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
        }

        private bool Reprocess => _reprocessOption?.HasValue() ?? false;
        private bool Resume => _resumeOption?.HasValue() ?? false;
        private IReadOnlyList<string> QueryNames => _queriesOption?.Values ?? new List<string>();

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

            await _processor.ProcessAsync(queries, Reprocess, token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
