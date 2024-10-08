// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public static class TestOutputHelperExtensions
    {
        public static void WriteTestCleanup(this ITestOutputHelper output)
        {
            output.WriteHorizontalRule();
            output.WriteLine("Beginning test clean-up.");
            output.WriteHorizontalRule();
        }

        public static void WriteHorizontalRule(this ITestOutputHelper output)
        {
            output.WriteLine(new string('-', 80));
        }

        public static ILoggerFactory GetLoggerFactory(this ITestOutputHelper output)
        {
            var factory = new LoggerFactory();
            factory.AddXunit(output);
            return factory;
        }

        public static ILogger<T> GetLogger<T>(this ITestOutputHelper output)
        {
            return output.GetLoggerFactory().CreateLogger<T>();
        }

        public static bool ShouldIgnoreMetricLog(LoggerTelemetryClient.MetricKey key)
        {
            return key.MetricId == MetricNames.MessageProcessorDurationMs
                || key.MetricId == MetricNames.BatchMessageProcessorDurationMs
                || key.MetricId.StartsWith(TableClientWithRetryContext.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(TelemetryHttpHandler.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(PackageWideEntityService.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(StorageLeaseService.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.Contains(QueryLoopMetrics.MetricIdSubstring, StringComparison.Ordinal);
        }

        internal static LoggerTelemetryClient GetTelemetryClient(this ITestOutputHelper output)
        {
            return new LoggerTelemetryClient(
                ShouldIgnoreMetricLog,
                output.GetLogger<LoggerTelemetryClient>());
        }
    }
}
