using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Logic;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public class ResetCursorCommand : ICommand
    {
        private readonly CursorService _cursorService;
        private readonly ILogger<ResetCursorCommand> _logger;
        private CommandArgument _nameArgument;

        public ResetCursorCommand(
            CursorService cursorService,
            ILogger<ResetCursorCommand> logger)
        {
            _cursorService = cursorService;
            _logger = logger;
        }

        public void Configure(CommandLineApplication app)
        {
            _nameArgument = app.Argument("name", "The name of the cursor to reset.");
        }

        private string Name => _nameArgument.Value;

        public async Task ExecuteAsync(CancellationToken token)
        {
            _logger.LogInformation("Resetting cursor {Name}...", Name);
            var cursor = await _cursorService.GetAsync(Name);
            if (cursor == null)
            {
                _logger.LogWarning("No cursor with that name exists.");
                var allNames = await _cursorService.GetAllNamesAsync();
                _logger.LogInformation(
                    $"The following {{Count}} cursor names exist:{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, allNames.Select(x => $" - {x}"))}",
                    allNames.Count);
            }
            else
            {
                await _cursorService.ResetValueAsync(Name);
                _logger.LogInformation("Done resetting cursor {Name}.", Name);
            }
        }

        public bool IsDatabaseRequired() => true;
        public bool IsInitializationRequired() => true;
        public bool IsSingleton() => true;
    }
}
