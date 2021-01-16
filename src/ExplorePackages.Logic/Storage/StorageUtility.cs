using System;
using System.Collections.Generic;

namespace Knapcode.ExplorePackages
{
    public class StorageUtility
    {
        public const int MaxBatchSize = 100;
        public const int MaxTakeCount = 1000;
        public const string PartitionKey = "PartitionKey";
        public const string RowKey = "RowKey";

        public static readonly IList<string> MinSelectColumns = new[] { PartitionKey, RowKey };

        public static string GenerateUniqueId()
        {
            return Guid.NewGuid().ToByteArray().ToTrimmedBase32();
        }

        public static StorageId GenerateDescendingId()
        {
            var descendingComponent = GetDescendingId(DateTimeOffset.UtcNow);
            var uniqueComponent = GenerateUniqueId();
            return new StorageId(descendingComponent, uniqueComponent);
        }

        public static string GetDescendingId(DateTimeOffset timestamp)
        {
            return (long.MaxValue - timestamp.Ticks).ToString("D20");
        }

        public static TimeSpan GetMessageDelay(int attemptCount)
        {
            return TimeSpan.FromSeconds(Math.Min(Math.Max(attemptCount, 0), 60));
        }
    }
}
