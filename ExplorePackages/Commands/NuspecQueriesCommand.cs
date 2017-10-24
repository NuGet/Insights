using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class NuspecQueriesCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly FindEmptyIdsNuspecQuery _findEmptyIds;
        private readonly FindRepositoriesNuspecQuery _findRepositories;
        private readonly FindPackageTypesNuspecQuery _findPackageTypes;
        private readonly ILogger _log;

        public NuspecQueriesCommand(
            PackagePathProvider pathProvider,
            FindEmptyIdsNuspecQuery findEmptyIds,
            FindRepositoriesNuspecQuery findRepositories,
            FindPackageTypesNuspecQuery findPackageTypes,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _findEmptyIds = findEmptyIds;
            _findRepositories = findRepositories;
            _findPackageTypes = findPackageTypes;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var nuspecProcessor = new NuspecQueryProcessor(
                _pathProvider,
                new List<INuspecQuery> { _findEmptyIds, _findRepositories, _findPackageTypes },
                _log);

            await nuspecProcessor.ProcessAsync(token);
        }
    }
}
