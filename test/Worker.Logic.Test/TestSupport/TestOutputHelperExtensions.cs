// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadLatestPackageLeaf;
using NuGet.Insights.Worker.TimedReprocess;

namespace NuGet.Insights.Worker
{
    public static class TestOutputHelperExtensions
    {
        public static bool ShouldIgnoreMetricLog(LoggerTelemetryClient.MetricKey key)
        {
            return Insights.TestOutputHelperExtensions.ShouldIgnoreMetricLog(key)
                || key.MetricId.StartsWith(AppendResultStorageService.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(TableScanMessageProcessor<LatestPackageLeaf>.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(EntityUpsertStorageService<ICatalogLeafItem, LatestPackageLeaf>.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(QueueStorageEnqueuer.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(TimedReprocessStorageService.MetricIdPrefix, StringComparison.Ordinal)
                || key.MetricId.StartsWith(TaskStateMessageProcessor<TableScanMessage<LatestPackageLeaf>>.MetricIdPrefix, StringComparison.Ordinal);
        }

        public static LoggerTelemetryClient GetTelemetryClient(this ITestOutputHelper output)
        {
            return new LoggerTelemetryClient(
                ShouldIgnoreMetricLog,
                output.GetLogger<LoggerTelemetryClient>());
        }
    }
}
