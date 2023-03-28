// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using System;

#nullable enable

namespace NuGet.Insights
{
    public static class LoggerExtensions
    {
        internal const string TransientPrefix = "[transient] ";

        public static Common.ILogger ToNuGetLogger(this ILogger logger)
        {
            return new StandardToNuGetLogger(logger);
        }

        public static void LogTransientWarning(this ILogger logger, EventId eventId, Exception? exception, string? message, params object?[] args)
        {
            logger.LogWarning(eventId, exception, TransientPrefix + message, args);
        }

        public static void LogTransientWarning(this ILogger logger, EventId eventId, string? message, params object?[] args)
        {
            logger.LogWarning(eventId, TransientPrefix + message, args);
        }

        public static void LogTransientWarning(this ILogger logger, Exception? exception, string? message, params object?[] args)
        {
            logger.LogWarning(exception, TransientPrefix + message, args);
        }

        public static void LogTransientWarning(this ILogger logger, string? message, params object?[] args)
        {
            logger.LogWarning(TransientPrefix + message, args);
        }
    }
}
