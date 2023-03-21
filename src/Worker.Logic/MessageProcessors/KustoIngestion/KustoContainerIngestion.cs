// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Azure;
using Azure.Data.Tables;

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class KustoContainerIngestion : ITableEntity
    {
        public static readonly string DefaultPartitionKey = string.Empty;

        public KustoContainerIngestion()
        {
        }

        public KustoContainerIngestion(string containerName)
        {
            PartitionKey = DefaultPartitionKey;
            RowKey = containerName;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string IngestionId { get; set; }
        public string StorageSuffix { get; set; }
        public KustoContainerIngestionState State { get; set; }
        public DateTimeOffset? Started { get; set; }

        public string GetContainerName()
        {
            return RowKey;
        }
    }
}
