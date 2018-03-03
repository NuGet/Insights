using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Commands
{
    public class V2ToDatabaseCommand : ICommand
    {
        private readonly V2ToDatabaseProcessor _processor;

        public V2ToDatabaseCommand(V2ToDatabaseProcessor processor)
        {
            _processor = processor;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _processor.UpdateAsync(V2OrderByTimestamp.Created);

            await _processor.UpdateAsync(V2OrderByTimestamp.LastEdited);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
