// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Tool
{
    public class MinimalConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new MinimalConsoleLogger(categoryName);
        }

        public void Dispose()
        {
        }
    }
}
