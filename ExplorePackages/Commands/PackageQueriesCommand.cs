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

        private CommandOption _reprocessAllOption;
        private CommandOption _resumeOption;

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
            _reprocessAllOption = app.Option(
                "--reprocess-all",
                "Reprocess all package query results.",
                CommandOptionType.NoValue);
            _resumeOption = app.Option(
                "--resume",
                "Resume the current reprocess operation.",
                CommandOptionType.NoValue);
        }

        private bool ReprocessAll => _reprocessAllOption?.HasValue() ?? false;
        private bool Resume => _resumeOption?.HasValue() ?? false;

        public async Task ExecuteAsync(CancellationToken token)
        {
            if (ReprocessAll && !Resume)
            {
                await _cursorService.ResetValueAsync(CursorNames.ReprocessPackageQueries);
            }

            await _processor.ProcessAsync(_queries.ToList(), ReprocessAll, token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
