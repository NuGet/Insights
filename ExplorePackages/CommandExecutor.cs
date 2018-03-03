using System;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Commands;
using NuGet.Common;

namespace Knapcode.ExplorePackages
{
    public class CommandExecutor
    {
        private readonly ICommand _command;
        private readonly ILogger _log;

        public CommandExecutor(ICommand command, ILogger log)
        {
            _command = command;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            var commandName = _command.GetType().Name;
            var suffix = "Command";
            if (commandName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                commandName = commandName.Substring(0, commandName.Length - suffix.Length);
            }
            var heading = $"===== {commandName.ToLowerInvariant()} =====";
            Console.WriteLine(heading);
            try
            {
                await _command.ExecuteAsync(token);
            }
            catch (Exception e)
            {
                _log.LogError("An exception occurred." + Environment.NewLine + e);
            }
            Console.WriteLine(new string('=', heading.Length));
            Console.WriteLine();
        }
    }
}
