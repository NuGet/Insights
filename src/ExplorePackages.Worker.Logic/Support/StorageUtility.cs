using System;

namespace Knapcode.ExplorePackages.Worker
{
    public class StorageUtility
    {
        public const int MaxBatchSize = 100;
        public const int MaxTakeCount = 1000;
        public const string PartitionKey = "PartitionKey";
        public const string RowKey = "RowKey";

        public static string GenerateUniqueId()
        {
            return Base32
                .ToBase32(Guid.NewGuid().ToByteArray())
                .TrimEnd('=')
                .ToLowerInvariant();
        }

        public static StorageId GenerateDescendingId()
        {
            var descendingComponent = (long.MaxValue - DateTimeOffset.UtcNow.Ticks).ToString("D20");
            var uniqueComponent = GenerateUniqueId();
            return new StorageId(descendingComponent, uniqueComponent);
        }

        public static TimeSpan GetMessageDelay(int attemptCount)
        {
            return TimeSpan.FromSeconds(Math.Min(Math.Max(attemptCount, 0), 60));
        }
    }
}
