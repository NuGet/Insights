// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using MessagePack;

namespace NuGet.Insights.ReferenceTracking
{
    [MessagePackObject]
    [DebuggerDisplay("SubjectReference: [{PartitionKey}/{RowKey}]")]
    public class SubjectReference : IReference, IEquatable<SubjectReference>
    {
        public SubjectReference(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        [Key(0)]
        public string PartitionKey { get; }
        [Key(1)]
        public string RowKey { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as SubjectReference);
        }

        public bool Equals(SubjectReference other)
        {
            return other != null &&
                   PartitionKey == other.PartitionKey &&
                   RowKey == other.RowKey;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PartitionKey, RowKey);
        }
    }
}
