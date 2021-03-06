using System;
using System.Collections.Generic;
using Knapcode.ExplorePackages.Worker;

namespace Knapcode.ExplorePackages.Website.Models
{
    public class CatalogScanViewModel
    {
        public CatalogScanDriverType DriverType { get; set; }
        public TimeSpan CursorAge => DateTimeOffset.UtcNow - Cursor.Value;
        public CursorTableEntity Cursor { get; set; }
        public IReadOnlyList<CatalogIndexScan> LatestScans { get; set; }

        public DateTimeOffset DefaultMax => DateTimeOffset.Parse("2015-02-01T06:22:45.8488496Z");

        public bool SupportsReprocess { get; set; }
        public bool? OnlyLatestLeavesSupport { get; set; }
    }
}
