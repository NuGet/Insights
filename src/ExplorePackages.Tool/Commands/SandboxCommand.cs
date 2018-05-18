using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
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

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
