using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Tool
{
    public class SandboxCommand : ICommand
    {
        public SandboxCommand()
        {
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public Task ExecuteAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsSingleton() => true;

    }
}
