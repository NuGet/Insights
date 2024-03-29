// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class TestEntity : ITableEntityWithClientRequestId, IEquatable<TestEntity>, IComparable<TestEntity>
    {
        public TestEntity()
        {
        }

        public TestEntity(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
            FieldA = partitionKey + "/" + rowKey;
            FieldB = rowKey + "/" + partitionKey;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }

        public string FieldA { get; set; }
        public string FieldB { get; set; }

        public int CompareTo([AllowNull] TestEntity other)
        {
            if (other == null)
            {
                return 1;
            }

            var partitionKeyCompare = StringComparer.Ordinal.Compare(PartitionKey, other.PartitionKey);
            if (partitionKeyCompare != 0)
            {
                return partitionKeyCompare;
            }

            return StringComparer.Ordinal.Compare(RowKey, other.RowKey);
        }

        public override string ToString()
        {
            return $"{PartitionKey}/{RowKey}";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TestEntity);
        }

        public bool Equals(TestEntity other)
        {
            return other != null &&
                   PartitionKey == other.PartitionKey &&
                   RowKey == other.RowKey &&
                   ETag == other.ETag &&
                   FieldA == other.FieldA &&
                   FieldB == other.FieldB;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PartitionKey, RowKey, ETag, FieldA, FieldB);
        }
    }
}
