using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool
{
    public class FetchCursorsCommand : ICommand
    {
        private readonly RemoteCursorClient _client;
        private readonly CursorService _cursorService;

        public FetchCursorsCommand(
            RemoteCursorClient client,
            CursorService cursorService)
        {
            _client = client;
            _cursorService = cursorService;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var flatContainer = await _client.GetFlatContainerAsync(token);
            await _cursorService.SetValueAsync(CursorNames.NuGetOrg.FlatContainer, flatContainer);

            var registrationCursor = await _client.GetRegistrationAsync(token);
            await _cursorService.SetValueAsync(CursorNames.NuGetOrg.Registration, registrationCursor);

            var searchServiceCursor = await _client.GetSearchAsync();
            await _cursorService.SetValueAsync(CursorNames.NuGetOrg.Search, searchServiceCursor);
        }

        public bool IsInitializationRequired()
        {
            return true;
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }

        public bool IsSingleton()
        {
            return true;
        }
    }
}
