using System;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogPageScanMessage
    {
        public string Url { get; set; }
        public DateTimeOffset Min { get; set; }
        public DateTimeOffset Max { get; set; }
    }
}
