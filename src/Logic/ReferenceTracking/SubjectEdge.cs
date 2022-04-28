// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MessagePack;

namespace NuGet.Insights.ReferenceTracking
{
    [MessagePackObject]
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class SubjectEdge : IReference, IEquatable<SubjectEdge>
    {
        public SubjectEdge(string partitionKey, string rowKey, byte[] data)
        {
            ReferenceTracker.GuardNoSeparator(nameof(partitionKey), partitionKey);
            ReferenceTracker.GuardNoSeparator(nameof(rowKey), rowKey);

            PartitionKey = partitionKey;
            RowKey = rowKey;
            Data = data;
        }

        public SubjectEdge()
        {
        }

        [Key(0)]
        public string PartitionKey { get; set; }
        [Key(1)]
        public string RowKey { get; set; }
        [Key(2)]
        public byte[] Data { get; set; }

        [IgnoreMember]
        public string DebuggerDisplay
        {
            get
            {
                var data = Encoding.UTF8.GetString(Data, 0, Math.Min(16, Data.Length));
                data += (Data.Length > 16 ? $"\"... {Data.Length} bytes" : "\"");

                return $"SubjectEdge [\"{PartitionKey}\"/\"{RowKey}\"] + data [\"{data}]";
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SubjectEdge);
        }

        public bool Equals(SubjectEdge other)
        {
            return other != null &&
                   PartitionKey == other.PartitionKey &&
                   RowKey == other.RowKey &&
                   Data.SequenceEqual(other.Data);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(PartitionKey);
            hashCode.Add(RowKey);
            foreach (var b in Data)
            {
                hashCode.Add(b);
            }
            return hashCode.ToHashCode();
        }
    }
}
