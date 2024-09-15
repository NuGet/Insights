// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings : NuGetInsightsSettings
    {
        public List<CatalogScanDriverType> DisabledDrivers { get; set; } = new List<CatalogScanDriverType>();

        public int OldCatalogIndexScansToKeep { get; set; } = 49;

        public bool AutoStartCatalogScanUpdate { get; set; } = false;

        public TimeSpan CatalogScanUpdateFrequency { get; set; } = TimeSpan.FromHours(6);

        /// <summary>
        /// If the duration that the catalog scan covers (max cursor minus min cursor) is less than or equal to this
        /// threshold, telemetry events for each catalog leaf will be emitted at various stages of processing. Note that
        /// leaf level telemetry can be a lot of data, so this threshold should be relatively low, e.g. a bit longer
        /// than the normal catalog scan update cadence. The goal of this configuration value is to prevent leaf-level
        /// telemetry when you are reprocessing the entire catalog. Set this to "00:00:00" if you want to disable this
        /// kind of telemetry. If you are not sure, set it to twice the value of <see cref="CatalogScanUpdateFrequency"/>.
        /// </summary>
        public TimeSpan LeafLevelTelemetryThreshold { get; set; } = TimeSpan.FromHours(12);

        public string CursorTableName { get; set; } = "cursors";

        public string CatalogIndexScanTableName { get; set; } = "catalogindexscans";
        public string CatalogPageScanTableNamePrefix { get; set; } = "catalogpagescan";
        public string CatalogLeafScanTableNamePrefix { get; set; } = "catalogleafscan";
    }
}
