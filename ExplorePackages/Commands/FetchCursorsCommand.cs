using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class FetchCursorsCommand : ICommand
    {
        private readonly RemoteCursorReader _reader;
        private readonly ILogger _log;

        public FetchCursorsCommand(RemoteCursorReader reader, ILogger log)
        {
            _reader = reader;
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
        {
            var cursorService = new CursorService();

            var cursors = await _reader.GetNuGetOrgCursors(token);

            foreach (var cursor in cursors)
            {
                await cursorService.SetAsync(cursor.Name, cursor.GetDateTimeOffset());
            }
        }

        public bool IsDatabaseRequired(IReadOnlyList<string> args)
        {
            return true;
        }
    }
}
