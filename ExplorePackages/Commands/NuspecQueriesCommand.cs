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
        private readonly FindMissingDependencyIdsNuspecQuery _findMissingDependencyIds;
        private readonly FindRepositoriesNuspecQuery _findRepositories;
        private readonly FindPackageTypesNuspecQuery _findPackageTypes;
        private readonly FindInvalidDependencyVersionsNuspecQuery _findInvalidDependencyVersions;
        private readonly FindMissingDependencyVersionsNuspecQuery _findMissingDependencyVersions;
        private readonly FindEmptyDependencyVersionsNuspecQuery _findEmptyDependencyVersions;
        private readonly ILogger _log;

        public NuspecQueriesCommand(
            PackagePathProvider pathProvider,
            FindMissingDependencyIdsNuspecQuery findMissingDependencyIds,
            FindRepositoriesNuspecQuery findRepositories,
            FindPackageTypesNuspecQuery findPackageTypes,
            FindInvalidDependencyVersionsNuspecQuery findInvalidDependencyVersions,
            FindMissingDependencyVersionsNuspecQuery findMissingDependencyVersions,
            FindEmptyDependencyVersionsNuspecQuery findEmptyDependencyVersions,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _findMissingDependencyIds = findMissingDependencyIds;
            _findRepositories = findRepositories;
            _findPackageTypes = findPackageTypes;
            _findInvalidDependencyVersions = findInvalidDependencyVersions;
            _findMissingDependencyVersions = findMissingDependencyVersions;
            _findEmptyDependencyVersions = findEmptyDependencyVersions;
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var nuspecProcessor = new NuspecQueryProcessor(
                _pathProvider,
                new List<INuspecQuery>
                {
                    _findMissingDependencyIds,
                    _findRepositories,
                    _findPackageTypes,
                    _findInvalidDependencyVersions,
                    _findMissingDependencyVersions,
                    _findEmptyDependencyVersions,
                },
                _log);

            await nuspecProcessor.ProcessAsync(token);
        }
    }
}
