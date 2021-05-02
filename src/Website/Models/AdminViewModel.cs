using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Website.Models
{
    public class AdminViewModel
    {
        public QueueViewModel WorkQueue { get; set; }
        public QueueViewModel ExpandQueue { get; set; }

        public DateTimeOffset DefaultMax { get; set; }
        public IReadOnlyList<CatalogScanViewModel> CatalogScans { get; set; }
        public IReadOnlyList<TimerState> TimerStates { get; set; }
    }
}
