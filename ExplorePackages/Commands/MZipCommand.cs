using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class MZipCommand : ICommand
    {
        private readonly MZipProcessor _processor;
        private readonly PackageCommitCollector _collector;

        public MZipCommand(
            MZipProcessor processor,
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
            await _collector.ProcessAsync(_processor, ProcessMode.TaskQueue, token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
