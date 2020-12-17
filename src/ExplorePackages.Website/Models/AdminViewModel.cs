using System.Collections.Generic;
using Knapcode.ExplorePackages.Worker;

namespace Knapcode.ExplorePackages.Website.Models
{
    public class AdminViewModel
    {
        public CursorTableEntity Cursor { get; set; }
        public IReadOnlyList<CatalogIndexScan> LatestScans { get; set; }
        public int ApproximateMessageCount { get; internal set; }
        public int AvailableMessageCountLowerBound { get; internal set; }
    }
}
