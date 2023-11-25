// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    /// <summary>
    /// Source: https://khalidabuhakmeh.com/logging-trace-output-using-ilogger-in-dotnet-applications
    /// </summary>
    public class LoggerTraceListener : TraceListener
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _defaultLogger;
        private readonly ConcurrentDictionary<string, ILogger> _loggers = new();

        public LoggerTraceListener(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _defaultLogger = loggerFactory.CreateLogger(nameof(LoggerTraceListener));
        }

        public override void TraceData(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, object? data)
        {
            if (data is string message)
            {
                GetLogger(source).Log(MapLevel(eventType), id, message);
            }
            else
            {
                GetLogger(source).Log(MapLevel(eventType), id, "{TraceData}", data);
            }
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            GetLogger(source).Log(MapLevel(eventType), id, message);
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
        {
            GetLogger(source).Log(MapLevel(eventType), id, format, args ?? Array.Empty<object>());
        }

        public override void Write(string? message)
        {
            _defaultLogger.LogInformation(message);
        }

        public override void WriteLine(string? message)
        {
            Write(message);
        }

        private ILogger GetLogger(string source)
        {
            return _loggers.GetOrAdd(
                source,
                static (s, factory) => factory.CreateLogger(nameof(LoggerTraceListener) + "." + s),
                _loggerFactory);
        }

        public override bool IsThreadSafe => true;

        private LogLevel MapLevel(TraceEventType eventType) => eventType switch
        {
            TraceEventType.Verbose => LogLevel.Debug,
            TraceEventType.Information => LogLevel.Information,
            TraceEventType.Critical => LogLevel.Critical,
            TraceEventType.Error => LogLevel.Error,
            TraceEventType.Warning => LogLevel.Warning,
            _ => LogLevel.Trace
        };
    }
}
