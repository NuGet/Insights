using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    /// <summary>
    /// Source:
    /// https://github.com/NuGet/NuGet.Jobs/blob/ac0e8b67b94893180848ba485d661e56edcac3d1/tests/Validation.PackageSigning.Core.Tests/Support/XunitLoggerProvider.cs
    /// </summary>
    public class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;
        private readonly LogLevel _minLevel;
        private readonly ConcurrentDictionary<LogLevel, int> _logLevelToCount;
        private readonly LogLevel _throwOn;

        public XunitLoggerProvider(ITestOutputHelper output)
            : this(output, LogLevel.Trace)
        {
        }

        public XunitLoggerProvider(ITestOutputHelper output, LogLevel minLevel)
        {
            _output = output;
            _minLevel = minLevel;
            _throwOn = LogLevel.None;
        }

        public XunitLoggerProvider(ITestOutputHelper output, LogLevel minLevel, ConcurrentDictionary<LogLevel, int> logLevelToCount, LogLevel throwOn)
        {
            _output = output;
            _minLevel = minLevel;
            _logLevelToCount = logLevelToCount;
            _throwOn = throwOn;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName, _minLevel, _logLevelToCount, _throwOn);
        }

        public void Dispose()
        {
        }
    }
}
