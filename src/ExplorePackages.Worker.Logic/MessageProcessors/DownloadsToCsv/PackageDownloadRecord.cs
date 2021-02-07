using System;

namespace Knapcode.ExplorePackages.Worker.DownloadsToCsv
{
    public partial record PackageDownloadRecord : ICsvRecord<PackageDownloadRecord>
    {
        public DateTimeOffset AsOfTimestamp { get; set; }
        public string LowerId { get; set; }
        public string Identity { get; set; }
        public string Id { get; set; }
        public string Version { get; set; }
        public long Downloads { get; set; }
        public long TotalDownloads { get; set; }
    }
}
