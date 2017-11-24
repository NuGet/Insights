using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;

namespace Knapcode.ExplorePackages.Commands
{
    public class DownloadsToDatabaseCommand : ICommand
    {
        private readonly PackageDownloadsToDatabaseProcessor _processor;

        public DownloadsToDatabaseCommand(PackageDownloadsToDatabaseProcessor processor)
        {
            _processor = processor;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await _processor.UpdateAsync();
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
