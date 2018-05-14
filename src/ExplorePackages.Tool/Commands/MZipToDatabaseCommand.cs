using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class MZipToDatabaseCommand : ICommand
    {
        private readonly MZipToDatabaseCommitCollector _collector;

        public MZipToDatabaseCommand(MZipToDatabaseCommitCollector collector)
        {
            _collector = collector;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _collector.ProcessAsync(ProcessMode.Sequentially, token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
