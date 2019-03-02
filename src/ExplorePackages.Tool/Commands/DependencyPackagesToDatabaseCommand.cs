using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class DependencyPackagesToDatabaseCommand : ICommand
    {
        private readonly DependencyPackagesToDatabaseCommitProcessor.Collector _collector;

        public DependencyPackagesToDatabaseCommand(
            DependencyPackagesToDatabaseCommitProcessor.Collector collector)
        {
            _collector = collector;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _collector.ProcessAsync(token);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;
    }
}
