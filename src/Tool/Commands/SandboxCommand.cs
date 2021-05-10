using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

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

        public async Task ExecuteAsync(CancellationToken token)
        {
            await Task.Yield();
        }

        public bool IsInitializationRequired()
        {
            return false;
        }

        public bool IsDatabaseRequired()
        {
            return false;
        }

        public bool IsSingleton()
        {
            return false;
        }
    }
}
