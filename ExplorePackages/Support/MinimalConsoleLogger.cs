using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Knapcode.ExplorePackages.Support
{
    public class MinimalConsoleLogger : ConsoleLogger
    {
        private static readonly Func<string, LogLevel, bool> _trueFilter = (c, l) => true;

        public MinimalConsoleLogger(string name)
            : base(name, _trueFilter, includeScopes: true)
        {
        }

        public override void WriteMessage(LogLevel logLevel, string logName, int eventId, string message, Exception exception)
        {
            ConsoleUtility.LogToConsole(logLevel, message);
        }
    }
}
