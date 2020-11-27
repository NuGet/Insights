using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public class CommandExecutor
    {
        private readonly ICommand _command;
        private readonly ISingletonService _singletonService;
        private readonly ILogger<CommandExecutor> _logger;

        public CommandExecutor(
            ICommand command,
            ISingletonService singletonService,
            ILogger<CommandExecutor> logger)
        {
            _command = command;
            _singletonService = singletonService;
            _logger = logger;
        }

        public async Task<bool> ExecuteAsync(CancellationToken token)
        {
            var commandName = _command.GetType().Name;
            var suffix = "Command";
            if (commandName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                commandName = commandName.Substring(0, commandName.Length - suffix.Length);
            }
            var heading = $"===== {commandName.ToLowerInvariant()} =====";
            _logger.LogInformation(heading);
            bool success;
            try
            {
                // Acquire the singleton lease.
                if (_command.IsSingleton())
                {
                    _logger.LogInformation("Ensuring that this job is a singleton.");
                    await _singletonService.AcquireOrRenewAsync();
                }

                await _command.ExecuteAsync(token);
                success = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An exception occurred.");
                success = false;
            }
            _logger.LogInformation(new string('=', heading.Length) + Environment.NewLine);
            return success;
        }
    }
}
