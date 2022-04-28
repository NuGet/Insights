// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace NuGet.Insights.ReferenceTracking
{
    [DebuggerDisplay("OwnerReference: [{PartitionKey}/{RowKey}]")]
    public class OwnerReference : IReference, IEquatable<OwnerReference>
    {
        public OwnerReference(string partitionKey, string rowKey)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        public string PartitionKey { get; }
        public string RowKey { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as OwnerReference);
        }

        public bool Equals(OwnerReference other)
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
