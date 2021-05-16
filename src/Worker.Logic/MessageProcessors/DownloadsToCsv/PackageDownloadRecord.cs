using System;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public partial record PackageDownloadRecord : ICsvRecord
    {
        [KustoIgnore]
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
