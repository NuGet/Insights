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
        private readonly PackageQueryProcessor _processor;
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

        public PackageQueriesCommand(
            PackageQueryProcessor processor,
            IEnumerable<IPackageQuery> queries,
            ILogger log)
        {
            _processor = processor;
            _queries = queries.ToList();
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await _processor.ProcessAsync(_queries, token);
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
