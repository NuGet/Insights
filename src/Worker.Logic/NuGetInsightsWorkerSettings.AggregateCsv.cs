// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public int AppendResultStorageBucketCount { get; set; } = 1000; // this is the maximum number of blobs fetched in a single request
        public int AppendResultBigModeRecordThreshold { get; set; } = 25_000;
        public int AppendResultBigModeSubdivisionSize { get; set; } = 10_000;

        public string CsvRecordTableNamePrefix { get; set; } = "csvrecord";
    }
}
