using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Commands
{
    public class MZipCommand : ICommand
    {
        private readonly MZipCollector _processor;

        public MZipCommand(MZipCollector processor)
        {
            _processor = processor;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await _processor.ExecuteAsync(token);
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
