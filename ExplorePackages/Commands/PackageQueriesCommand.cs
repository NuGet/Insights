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
        private readonly PackagePathProvider _pathProvider;
        private readonly List<IPackageQuery> _queries;
        private readonly ILogger _log;

        public PackageQueriesCommand(
            PackagePathProvider pathProvider,
            IEnumerable<IPackageQuery> queries,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _queries = queries.ToList();
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var processor = new PackageQueryProcessor(
                _pathProvider,
                _queries,
                _log);

            await processor.ProcessAsync(token);
        }
    }
}
