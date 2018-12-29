using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class DependenciesToDatabaseCommand : ICommand
    {
        private readonly DependenciesToDatabaseCommitCollector _collector;

        public DependenciesToDatabaseCommand(DependenciesToDatabaseCommitCollector collector)
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

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsReadOnly() => false;
    }
}
