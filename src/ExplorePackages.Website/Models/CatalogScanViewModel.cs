using System;
using System.Collections.Generic;
using Knapcode.ExplorePackages.Worker;

namespace Knapcode.ExplorePackages.Website.Models
{
    public class CatalogScanViewModel
    {
        public CatalogScanType Type { get; set; }
        public TimeSpan CursorAge => DateTimeOffset.UtcNow - Cursor.Value;
        public CursorTableEntity Cursor { get; set; }
        public IReadOnlyList<CatalogIndexScan> LatestScans { get; set; }
    }
}
