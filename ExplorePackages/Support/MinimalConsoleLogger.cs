using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NuGetLogLevel = NuGet.Common.LogLevel;

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
            ConsoleUtility.LogToConsole(GetLogLevel(logLevel), message);
        }

        private static NuGetLogLevel GetLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return NuGetLogLevel.Debug;
                case LogLevel.Debug:
                    return NuGetLogLevel.Verbose;
                case LogLevel.Information:
                    return NuGetLogLevel.Minimal;
                case LogLevel.Warning:
                    return NuGetLogLevel.Warning;
                case LogLevel.Error:
                    return NuGetLogLevel.Error;
                default:
                    return NuGetLogLevel.Debug;
            }
        }
    }
}
