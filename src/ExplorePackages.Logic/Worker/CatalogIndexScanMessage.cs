using System;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class CatalogIndexScanMessage
    {
        public DateTimeOffset? Min { get; set; }
        public DateTimeOffset? Max { get; set; }
    }
}
