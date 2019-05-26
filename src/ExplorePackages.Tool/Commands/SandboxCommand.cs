using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        private readonly BlobStorageMigrator _blobStorageMigrator;

        public SandboxCommand(BlobStorageMigrator blobStorageMigrator)
        {
            _blobStorageMigrator = blobStorageMigrator;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var source = new BlobStorageMigrationSource(
                "core.windows.net",
                "explorepackages",
                "?st=2019-05-25T20%3A35%3A03Z&se=2019-05-26T20%3A35%3A03Z&sp=rl&sv=2018-03-28&sr=c&sig=lB%2FlkaB5fYixrQ0HTEZ%2BIX%2Foxmx%2Bi9yjd87UY8X8Er0%3D",
                "packages2");

            await _blobStorageMigrator.MigrateAsync(source);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
