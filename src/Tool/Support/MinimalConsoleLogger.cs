// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Tool
{
    public class MinimalConsoleLogger : ILogger
    {
        private readonly Stopwatch _stopwatch;

        public MinimalConsoleLogger(string name)
        {
            _stopwatch = Stopwatch.StartNew();
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
            messageBuilder.AppendFormat(CultureInfo.InvariantCulture, "[{0:F3}] ", _stopwatch.Elapsed.TotalSeconds);
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
