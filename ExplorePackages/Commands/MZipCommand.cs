using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class MZipCommand : ICommand
    {
        private readonly MZipCollector _processor;

        public MZipCommand(MZipCollector processor)
        {
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _processor.ExecuteAsync(token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
