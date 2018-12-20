using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class SandboxCommand : ICommand
    {
        private readonly BlobStorageMigrator _migrator;

        public SandboxCommand(BlobStorageMigrator migrator)
        {
            _migrator = migrator;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var source = new BlobStorageMigrationSource(
                "core.windows.net",
                "explorepackages",
                "SAS_TOKEN",
                "packages");

            await _migrator.MigrateAsync(source);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
