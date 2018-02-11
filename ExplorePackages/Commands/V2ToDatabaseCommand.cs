using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Commands
{
    public class V2ToDatabaseCommand : ICommand
    {
        private readonly V2ToDatabaseProcessor _processor;

        public V2ToDatabaseCommand(V2ToDatabaseProcessor processor)
        {
            _processor = processor;
        }
                
        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await _processor.UpdateAsync(V2OrderByTimestamp.Created);

            await _processor.UpdateAsync(V2OrderByTimestamp.LastEdited);
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
