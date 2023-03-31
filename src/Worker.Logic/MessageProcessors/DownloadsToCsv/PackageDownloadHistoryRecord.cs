// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    [NoKustoDDL]
    public partial record PackageDownloadHistoryRecord : IPackageDownloadRecord, ICsvRecord
    {
        public DateTimeOffset AsOfTimestamp { get; set; }

        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }
        public long Downloads { get; set; }
        public long TotalDownloads { get; set; }
    }
}
