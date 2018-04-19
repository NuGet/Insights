using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class DependenciesToDatabaseCommand : ICommand
    {
        private readonly PackageCommitCollector _collector;
        private readonly DependenciesToDatabaseProcessor _processor;

        public DependenciesToDatabaseCommand(
            PackageCommitCollector collector,
            DependenciesToDatabaseProcessor processor)
        {
            _collector = collector;
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _collector.ProcessAsync(_processor, token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
