using System;
using Azure;
using Azure.Data.Tables;

namespace Knapcode.ExplorePackages.Worker
{
    public class CursorTableEntity : ITableEntity
    {
        public static readonly DateTimeOffset Min = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public CursorTableEntity()
        {
        }

        public CursorTableEntity(string name)
        {
            PartitionKey = string.Empty;
            RowKey = name;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public DateTimeOffset Value { get; set; } = Min;

        public string GetName()
        {
            return RowKey;
        }
    }
}
