// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    /// <summary>
    /// Source:
    /// https://github.com/NuGet/NuGet.Jobs/blob/ac0e8b67b94893180848ba485d661e56edcac3d1/tests/Validation.PackageSigning.Core.Tests/Support/XunitLogger.cs
    /// </summary>
    public class XunitLogger : ILogger
    {
        private static readonly Stopwatch SinceStart = Stopwatch.StartNew();

        private readonly LogLevel _minLogLevel;
        private readonly ITestOutputHelper _output;
        private readonly ConcurrentDictionary<LogLevel, int> _logLevelToCount;
        private readonly Func<LogLevel, string, LogLevel> _transformLogLevel;
        private readonly LogLevel _throwOn;
        private readonly LimitedConcurrentQueue<string> _logMessages;

        public XunitLogger(
            ITestOutputHelper output,
            LogLevel minLogLevel,
            ConcurrentDictionary<LogLevel, int> logLevelToCount,
            Func<LogLevel, string, LogLevel> transformLogLevel,
            LogLevel throwOn,
            LimitedConcurrentQueue<string> logMessages)
        {
            _minLogLevel = minLogLevel;
            _output = output;
            _logLevelToCount = logLevelToCount;
            _transformLogLevel = transformLogLevel;
            _throwOn = throwOn;
            _logMessages = logMessages;
        }

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            var mappedLevel = logLevel;

            if (_transformLogLevel != null)
            {
                mappedLevel = _transformLogLevel(logLevel, message);
            }

            if (_logLevelToCount != null)
            {
                _logLevelToCount.AddOrUpdate(mappedLevel, 1, (_, v) => v + 1);
            }

            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (_logMessages is not null)
            {
                _logMessages.Enqueue(message, limit => _output.WriteLine($"The log message queue has exceeded its limit of {limit}. Older messages will be dropped."));
            }

            try
            {
                var abbreviateLogLevel = Abbreviate(logLevel);
                if (logLevel != mappedLevel)
                {
                    abbreviateLogLevel += ">" + Abbreviate(mappedLevel);
                }

                _output.WriteLine(
                    "[{0:F3}] [{1}] {2}{3}{4}",
                    SinceStart.Elapsed.TotalSeconds,
                    abbreviateLogLevel,
                    message,
                    exception is not null ? Environment.NewLine : string.Empty,
                    exception is not null ? exception : string.Empty);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("There is no currently active test.", StringComparison.Ordinal))
            {
                // Ignore this failure. I've seen cases where an HttpClientFactory timer logs at a strange time.
            }

            if (mappedLevel >= _throwOn)
            {
                throw new InvalidOperationException($"Failing early due to an {mappedLevel} log.");
            }
        }

        private static string Abbreviate(LogLevel logLevel)
        {
            return logLevel.ToString().Substring(0, 3).ToUpperInvariant();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLogLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new NullScope();
        }

        private class NullScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
