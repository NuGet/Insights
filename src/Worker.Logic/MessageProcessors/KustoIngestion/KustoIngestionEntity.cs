// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.Serialization;
using Azure;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoIngestionEntity : ITableEntityWithClientRequestId
    {
        public static readonly string DefaultPartitionKey = string.Empty;

        public KustoIngestionEntity()
        {
        }

        public KustoIngestionEntity(string ingestionId, string storageSuffix)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = ingestionId;
            StorageSuffix = storageSuffix;
            Created = DateTimeOffset.UtcNow;
            State = KustoIngestionState.Created;
        }

        [IgnoreDataMember]
        public string IngestionId => RowKey;

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public Guid? ClientRequestId { get; set; }

        public string StorageSuffix { get; set; }
        public int AttemptCount { get; set; }
        public KustoIngestionState State { get; set; }
        public DateTimeOffset Created { get; set; }
        public DateTimeOffset? Started { get; set; }
        public DateTimeOffset? Completed { get; set; }
    }
}
