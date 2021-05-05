using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.WebUtilities;

namespace Knapcode.ExplorePackages
{
    public class StorageUtility
    {
        public const int MaxBatchSize = 100;
        public const int MaxTakeCount = 1000;

        public const string PartitionKey = "PartitionKey";
        public const string RowKey = "RowKey";
        public const string Timestamp = "Timestamp";
        public const string ETag = "ETag";

        public const string EmulatorConnectionString = "UseDevelopmentStorage=true";

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
            return GetMessageDelay(attemptCount, factor: 1);
        }

        public static TimeSpan GetMessageDelay(int attemptCount, int factor)
        {
            return TimeSpan.FromSeconds(Math.Min(Math.Max(attemptCount * factor, 0), 60));
        }

        public static DateTimeOffset GetSasExpiry(string sas)
        {
            DateTimeOffset sasExpiry;
            var parsedSas = QueryHelpers.ParseQuery(sas);
            var expiry = parsedSas["se"].Single();
            sasExpiry = DateTimeOffset.Parse(expiry);
            return sasExpiry;
        }
    }
}
