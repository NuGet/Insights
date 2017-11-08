using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class NuspecQueriesCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly List<INuspecQuery> _queries;
        private readonly ILogger _log;

        public NuspecQueriesCommand(
            PackagePathProvider pathProvider,
            IEnumerable<INuspecQuery> queries,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _queries = queries.ToList();
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var nuspecProcessor = new NuspecQueryProcessor(
                _pathProvider,
                _queries,
                _log);

            await nuspecProcessor.ProcessAsync(token);
        }
    }
}
