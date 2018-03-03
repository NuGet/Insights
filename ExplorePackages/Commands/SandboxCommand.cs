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
        private readonly PackageService _packageService;
        private readonly PackagePathProvider _pathProvider;
        private readonly MZipFormat _mZipFormat;

        public SandboxCommand(
            PackageService packageService,
            PackagePathProvider pathProvider,
            MZipFormat mZipFormat)
        {
            _packageService = packageService;
            _pathProvider = pathProvider;
            _mZipFormat = mZipFormat;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();

            int commitCount;
            do
            {
                var commits = await _packageService.GetPackageCommitsAsync(
                    DateTimeOffset.MinValue,
                    DateTimeOffset.MaxValue);
                commitCount = commits.Count;
                
                foreach (var commit in commits)
                {
                    foreach (var package in commit.Packages)
                    {
                        var path = _pathProvider.GetLatestMZipPath(
                            package.PackageRegistration.Id,
                            package.Version);

                        using (var fileStream = new FileStream(path, FileMode.Open))
                        {
                            var zipStream = await _mZipFormat.ReadAsync(fileStream);
                            Console.WriteLine($"{package.PackageRegistration.Id} {package.Version} {zipStream.Length}");
                        }
                    }
                }
            }
            while (commitCount > 0);

        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
