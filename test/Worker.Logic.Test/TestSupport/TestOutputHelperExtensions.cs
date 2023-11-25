// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public static class TestOutputHelperExtensions
    {
        public static bool ShouldIgnoreMetricLog(LoggerTelemetryClient.MetricKey key)
        {
            return key.MetricId.Contains(QueryLoopMetrics.MetricIdSubstring, StringComparison.Ordinal)
                || key.MetricId == GenericMessageProcessor.MessageProcessorDurationMsMetricId
                || key.MetricId == GenericMessageProcessor.BatchMessageProcessorDurationMsMetricId;
        }

        public static LoggerTelemetryClient GetTelemetryClient(this ITestOutputHelper output)
        {
            return new LoggerTelemetryClient(
                ShouldIgnoreMetricLog,
                output.GetLogger<LoggerTelemetryClient>());
        }
    }
}
