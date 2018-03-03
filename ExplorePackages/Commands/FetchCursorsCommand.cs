using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class FetchCursorsCommand : ICommand
    {
        private readonly RemoteCursorService _service;
        private readonly ILogger _log;

        public FetchCursorsCommand(RemoteCursorService service, ILogger log)
        {
            _service = service;
            _log = log;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            await _service.UpdateNuGetOrgCursors(token);
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }
    }
}
