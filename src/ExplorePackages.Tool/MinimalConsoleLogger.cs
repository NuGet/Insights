using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public class MinimalConsoleLogger : ILogger
    {
        public MinimalConsoleLogger(string name)
        {
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullDisposable.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append(formatter(state, exception));
            if (exception != null)
            {
                messageBuilder.AppendLine();
                messageBuilder.Append(exception);
            }

            ConsoleUtility.LogToConsole(logLevel, messageBuilder.ToString());
        }

        private class NullDisposable : IDisposable
        {
            public static NullDisposable Instance { get; } = new NullDisposable();

            public void Dispose()
            {
            }
        }
    }
}
