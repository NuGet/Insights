using System;
using System.Collections.Generic;
using Knapcode.ExplorePackages.Worker;

namespace Knapcode.ExplorePackages.Website.Models
{
    public class AdminViewModel
    {
        public TimeSpan CursorAge => DateTimeOffset.UtcNow - Cursor.Value;
        public CursorTableEntity Cursor { get; set; }
        public IReadOnlyList<CatalogIndexScan> LatestScans { get; set; }
        public int ApproximateMessageCount { get; set; }
        public int AvailableMessageCountLowerBound { get; set; }
        public bool AvailableMessageCountIsExact { get; set; }
        public int PoisonApproximateMessageCount { get; set; }
        public int PoisonAvailableMessageCountLowerBound { get; set; }
        public bool PoisonAvailableMessageCountIsExact { get; set; }
    }
}
