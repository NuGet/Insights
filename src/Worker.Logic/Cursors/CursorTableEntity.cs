// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker
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

        [IgnoreDataMember]
        public string Name => RowKey;

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public DateTimeOffset Value { get; set; } = Min;
    }
}
