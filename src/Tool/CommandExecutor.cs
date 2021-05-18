// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Tool
{
    public class CommandExecutor
    {
        private readonly ICommand _command;
        private readonly ILogger<CommandExecutor> _logger;

        public CommandExecutor(
            ICommand command,
            ILogger<CommandExecutor> logger)
        {
            _command = command;
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
