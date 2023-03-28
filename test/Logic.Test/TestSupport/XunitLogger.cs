// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

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
        private readonly ConcurrentQueue<string> _logMessages;

        public XunitLogger(
            ITestOutputHelper output,
            LogLevel minLogLevel,
            ConcurrentDictionary<LogLevel, int> logLevelToCount,
            Func<LogLevel, string, LogLevel> transformLogLevel,
            LogLevel throwOn,
            ConcurrentQueue<string> logMessages)
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

            _logMessages?.Enqueue(message);

            try
            {
                var abbreviateLogLevel = Abbreviate(logLevel);
                if (logLevel != mappedLevel)
                {
                    abbreviateLogLevel += ">" + Abbreviate(mappedLevel);
                }
                _output.WriteLine($"[{SinceStart.Elapsed.TotalSeconds:F3}] [{abbreviateLogLevel}] {message}");
                if (exception != null)
                {
                    _output.WriteLine(exception.ToString());
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("There is no currently active test."))
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
