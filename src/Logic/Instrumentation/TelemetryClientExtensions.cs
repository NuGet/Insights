// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace NuGet.Insights
{
    public static class TelemetryClientExtensions
    {
        public static readonly IDictionary<string, string> NoDimensions = new Dictionary<string, string>();

        public static void TrackMetric(this ITelemetryClient client, string name, double value)
        {
            client.TrackMetric(name, value, NoDimensions);
        }

        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(
                telemetryClient,
                sourceFilePath,
                memberName);
        }

        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(
                telemetryClient,
                dimension1Name,
                dimension1Value,
                sourceFilePath,
                memberName);
        }

        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            string dimension2Name,
            string dimension2Value,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(
                telemetryClient,
                dimension1Name,
                dimension1Value,
                dimension2Name,
                dimension2Value,
                sourceFilePath,
                memberName);
        }

        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            string dimension2Name,
            string dimension2Value,
            string dimension3Name,
            string dimension3Value,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(
                telemetryClient,
                dimension1Name,
                dimension1Value,
                dimension2Name,
                dimension2Value,
                dimension3Name,
                dimension3Value,
                sourceFilePath,
                memberName);
        }

        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            string dimension1Name,
            string dimension1Value,
            string dimension2Name,
            string dimension2Value,
            string dimension3Name,
            string dimension3Value,
            string dimension4Name,
            string dimension4Value,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(
                telemetryClient,
                dimension1Name,
                dimension1Value,
                dimension2Name,
                dimension2Value,
                dimension3Name,
                dimension3Value,
                dimension4Name,
                dimension4Value,
                sourceFilePath,
                memberName);
        }
    }
}
