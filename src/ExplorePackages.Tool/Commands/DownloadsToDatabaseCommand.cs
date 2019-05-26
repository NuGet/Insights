using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool
{
    public class DownloadsToDatabaseCommand : ICommand
    {
        private readonly PackageDownloadsToDatabaseProcessor _processor;

        public DownloadsToDatabaseCommand(PackageDownloadsToDatabaseProcessor processor)
        {
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _processor.UpdateAsync();
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
