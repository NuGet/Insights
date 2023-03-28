// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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
        private readonly Func<LogLevel, string, LogLevel> _transformLogLevel;
        private readonly LogLevel _throwOn;
        private readonly ConcurrentQueue<string> _logMessages;

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

        public XunitLoggerProvider(
            ITestOutputHelper output,
            LogLevel minLevel,
            ConcurrentDictionary<LogLevel, int> logLevelToCount,
            Func<LogLevel, string, LogLevel> transformLogLevel,
            LogLevel throwOn,
            ConcurrentQueue<string> logMessages)
        {
            _output = output;
            _minLevel = minLevel;
            _logLevelToCount = logLevelToCount;
            _transformLogLevel = transformLogLevel;
            _throwOn = throwOn;
            _logMessages = logMessages;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, _minLevel, _logLevelToCount, _transformLogLevel, _throwOn, _logMessages);
        }

        public void Dispose()
        {
        }
    }
}
