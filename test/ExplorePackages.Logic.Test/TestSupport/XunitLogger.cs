using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    /// <summary>
    /// Source:
    /// https://github.com/NuGet/NuGet.Jobs/blob/ac0e8b67b94893180848ba485d661e56edcac3d1/tests/Validation.PackageSigning.Core.Tests/Support/XunitLogger.cs
    /// </summary>
    public class XunitLogger : ILogger
    {
        private static readonly char[] NewLineChars = new[] { '\r', '\n' };
        private readonly string _category;
        private readonly LogLevel _minLogLevel;
        private readonly ITestOutputHelper _output;
        private readonly ConcurrentDictionary<LogLevel, int> _logLevelToCount;

        public XunitLogger(ITestOutputHelper output, string category, LogLevel minLogLevel)
        {
            _minLogLevel = minLogLevel;
            _category = category;
            _output = output;
            _logLevelToCount = null;
        }

        public XunitLogger(ITestOutputHelper output, string category, LogLevel minLogLevel, ConcurrentDictionary<LogLevel, int> logLevelToCount)
        {
            _minLogLevel = minLogLevel;
            _category = category;
            _output = output;
            _logLevelToCount = logLevelToCount;
        }

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (_logLevelToCount != null)
            {
                _logLevelToCount.AddOrUpdate(logLevel, 1, (_, v) => v + 1);
            }

            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            _output.WriteLine(message);
            if (exception != null)
            {
                _output.WriteLine(exception.ToString());
            }
        }

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= _minLogLevel;

        public IDisposable BeginScope<TState>(TState state)
            => new NullScope();

        private class NullScope : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
