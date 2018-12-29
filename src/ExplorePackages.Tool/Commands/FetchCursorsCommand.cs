using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool.Commands
{
    public class FetchCursorsCommand : ICommand
    {
        private readonly RemoteCursorService _service;

        public FetchCursorsCommand(RemoteCursorService service)
        {
            _service = service;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _service.UpdateNuGetOrgCursors(token);
        }

        public bool IsInitializationRequired() => true;
        public bool IsDatabaseRequired() => true;
        public bool IsReadOnly() => false;
    }
}
