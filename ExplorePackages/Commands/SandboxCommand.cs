using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using Knapcode.MiniZip;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly PackageDependencyService _packageDependencyService;
        private readonly NuspecProvider _nuspecProvider;

        public SandboxCommand(
            PackageDependencyService packageDependencyService,
            NuspecProvider nuspecProvider)
        {
            _packageDependencyService = packageDependencyService;
            _nuspecProvider = nuspecProvider;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var packageIdentity = new PackageIdentity("NuGet.Packaging", "4.6.2");
            var nuspec = _nuspecProvider.GetNuspec(packageIdentity.Id, packageIdentity.Version);
            var dependencyGroups = NuspecUtility.GetParsedDependencyGroups(nuspec.Document);
            var packageDependencyGroups = new PackageDependencyGroups(
                packageIdentity,
                dependencyGroups);

            await _packageDependencyService.AddDependenciesAsync(new[] { packageDependencyGroups });
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
