using System;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public class TableStorageUtility
    {
        public const int MaxBatchSize = 100;
        public const int MaxTakeCount = 1000;
        public const string PartitionKey = "PartitionKey";
        public const string RowKey = "RowKey";

        public static string GenerateDescendingId()
        {
            var descendingComponent = (long.MaxValue - DateTimeOffset.UtcNow.Ticks).ToString("D20");
            var uniqueComponent = Guid.NewGuid().ToString("N");
            return descendingComponent + "-" + uniqueComponent;
        }
    }
}
