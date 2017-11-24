using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
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

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            await _service.UpdateNuGetOrgCursors(token);
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
