// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;
using StandardLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace NuGet.Insights
{
    public class StandardToNuGetLogger : LoggerBase
    {
        private readonly Microsoft.Extensions.Logging.ILogger _logger;

        public StandardToNuGetLogger(Microsoft.Extensions.Logging.ILogger logger) : base(LogLevel.Debug)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override void Log(ILogMessage message)
        {
            if ((int)message.Level >= (int)VerbosityLevel)
            {
                _logger.Log(
                    logLevel: GetLogLevel(message.Level),
                    eventId: 0,
                    state: message,
                    exception: null,
                    formatter: (s, e) => s.Message);
            }
        }

        public override Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }

        private static StandardLogLevel GetLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Debug:
                    return StandardLogLevel.Trace;
                case LogLevel.Verbose:
                    return StandardLogLevel.Debug;
                case LogLevel.Information:
                    return StandardLogLevel.Information;
                case LogLevel.Minimal:
                    return StandardLogLevel.Information;
                case LogLevel.Warning:
                    return StandardLogLevel.Warning;
                case LogLevel.Error:
                    return StandardLogLevel.Error;
                default:
                    return StandardLogLevel.Trace;
            }
        }
    }
}
