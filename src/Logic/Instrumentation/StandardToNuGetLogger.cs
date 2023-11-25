// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;
using IStandardLogger = Microsoft.Extensions.Logging.ILogger;
using NuGetLogLevel = NuGet.Common.LogLevel;
using StandardLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace NuGet.Insights
{
    public class StandardToNuGetLogger : LoggerBase
    {
        private readonly IStandardLogger _logger;

        public StandardToNuGetLogger(IStandardLogger logger) : base(NuGetLogLevel.Debug)
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

        private static StandardLogLevel GetLogLevel(NuGetLogLevel logLevel)
        {
            switch (logLevel)
            {
                case NuGetLogLevel.Debug:
                    return StandardLogLevel.Trace;
                case NuGetLogLevel.Verbose:
                    return StandardLogLevel.Debug;
                case NuGetLogLevel.Information:
                    return StandardLogLevel.Information;
                case NuGetLogLevel.Minimal:
                    return StandardLogLevel.Information;
                case NuGetLogLevel.Warning:
                    return StandardLogLevel.Warning;
                case NuGetLogLevel.Error:
                    return StandardLogLevel.Error;
                default:
                    return StandardLogLevel.Trace;
            }
        }
    }
}
