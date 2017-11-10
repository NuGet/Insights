using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Commands
{
    public class PackageQueriesCommand : ICommand
    {
        private readonly PackageQueryProcessor _processor;

        public PackageQueriesCommand(PackageQueryProcessor processor)
        {
            _processor = processor;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await _processor.ProcessAsync(token);
        }
    }
}
