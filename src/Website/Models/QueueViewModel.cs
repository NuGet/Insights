using NuGet.Insights.Worker;

namespace NuGet.Insights.Website.Models
{
    public class QueueViewModel
    {
        public QueueType QueueType { get; set; }
        public int ApproximateMessageCount { get; set; }
        public int AvailableMessageCountLowerBound { get; set; }
        public bool AvailableMessageCountIsExact { get; set; }
        public int PoisonApproximateMessageCount { get; set; }
        public int PoisonAvailableMessageCountLowerBound { get; set; }
        public bool PoisonAvailableMessageCountIsExact { get; set; }
    }
}
