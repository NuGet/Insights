// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.CompilerServices;

namespace NuGet.Insights
{
    public static class TelemetryClientExtensions
    {
        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(telemetryClient, sourceFilePath, memberName);
        }
    }
}
