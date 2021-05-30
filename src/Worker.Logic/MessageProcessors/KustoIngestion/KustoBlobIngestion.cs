// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoBlobIngestion : ITableEntity
    {
        public KustoBlobIngestion()
        {
        }

        public KustoBlobIngestion(string containerName, string blobName)
        {
            PartitionKey = containerName;
            RowKey = blobName;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string IngestionId { get; set; }
        public string StorageSuffix { get; set; }
        public KustoBlobIngestionState State { get; set; }
        public long RawSizeBytes { get; set; }
        public string StatusUrl { get; set; }
        public Guid SourceId { get; set; }

        public string GetContainerName()
        {
            return PartitionKey;
        }

        public string GetBlobName()
        {
            return RowKey;
        }
    }
}
