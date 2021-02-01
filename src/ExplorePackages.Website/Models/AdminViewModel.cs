using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Website.Models
{
    public class AdminViewModel
    {
        public int ApproximateMessageCount { get; set; }
        public int AvailableMessageCountLowerBound { get; set; }
        public bool AvailableMessageCountIsExact { get; set; }
        public int PoisonApproximateMessageCount { get; set; }
        public int PoisonAvailableMessageCountLowerBound { get; set; }
        public bool PoisonAvailableMessageCountIsExact { get; set; }

        public IReadOnlyList<CatalogScanViewModel> CatalogScans { get; set; }

        public bool IsDownloadsToCsvRunning { get; set; }
        public bool IsOwnersToCsvRunning { get; set; }
    }
}
