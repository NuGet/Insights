// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public partial record PackageDownloadRecord : IPackageDownloadRecord<PackageDownloadRecord>
    {
        [KustoIgnore]
        public DateTimeOffset AsOfTimestamp { get; set; }

        public string LowerId { get; set; }

        [KustoPartitionKey]
        public string Identity { get; set; }

        public string Id { get; set; }
        public string Version { get; set; }

        [Required]
        public long Downloads { get; set; }

        [Required]
        public long TotalDownloads { get; set; }
    }
}
