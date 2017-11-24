using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class PackageQueriesCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly PackageQueryProcessor _processor;
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

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

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var argList = args.ToList();
            var queries = _queries.ToList();

            var reprocessAll = HasReprocessAllArg(argList);
            var resume = HasResumeArg(argList);
            if (reprocessAll && !resume)
            {
                await _cursorService.ResetValueAsync(CursorNames.ReprocessPackageQueries);
            }

            await _processor.ProcessAsync(queries, reprocessAll, token);
        }

        private bool HasReprocessAllArg(List<string> argList)
        {
            return ArgsUtility.HasArg(argList, "-reprocessall");
        }

        private bool HasResumeArg(List<string> argList)
        {
            return ArgsUtility.HasArg(argList, "-resume");
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
