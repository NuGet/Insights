// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class LoggerExtensions
    {
        public static Common.ILogger ToNuGetLogger(this Microsoft.Extensions.Logging.ILogger logger)
        {
            return new StandardToNuGetLogger(logger);
        }
    }
}
