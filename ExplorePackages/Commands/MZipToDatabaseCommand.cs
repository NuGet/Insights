using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class MZipToDatabaseCommand : ICommand
    {
        private readonly MZipToDatabaseProcessor _processor;
        private readonly PackageCommitCollector _collector;

        public MZipToDatabaseCommand(
            MZipToDatabaseProcessor processor,
            PackageCommitCollector collector)
        {
            _processor = processor;
            _collector = collector;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _collector.ProcessAsync(_processor, ProcessMode.Sequentially, token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
