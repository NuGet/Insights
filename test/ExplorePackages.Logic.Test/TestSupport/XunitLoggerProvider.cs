using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
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

        public XunitLoggerProvider(ITestOutputHelper output)
            : this(output, LogLevel.Trace)
        {
        }

        public XunitLoggerProvider(ITestOutputHelper output, LogLevel minLevel)
        {
            _output = output;
            _minLevel = minLevel;
        }

        public XunitLoggerProvider(ITestOutputHelper output, LogLevel minLevel, ConcurrentDictionary<LogLevel, int> logLevelToCount)
        {
            _output = output;
            _minLevel = minLevel;
            _logLevelToCount = logLevelToCount;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName, _minLevel, _logLevelToCount);
        }

        public void Dispose()
        {
        }
    }
}
