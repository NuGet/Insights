// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;

namespace NuGet.Insights.Worker
{
    public class TableScanMessage<T> : ITaskStateMessage where T : ITableEntity
    {
        [JsonPropertyName("b")]
        public DateTimeOffset Started { get; set; }

        [JsonPropertyName("ts")]
        public TaskStateKey TaskStateKey { get; set; }

        [JsonPropertyName("ac")]
        public int AttemptCount { get; set; }

        [JsonPropertyName("t")]
        public TableScanDriverType DriverType { get; set; }

        [JsonPropertyName("n")]
        public string TableName { get; set; }

        [JsonPropertyName("s")]
        public TableScanStrategy Strategy { get; set; }

        [JsonPropertyName("c")]
        public int TakeCount { get; set; }

        [JsonPropertyName("p")]
        public string PartitionKeyPrefix { get; set; }

        [JsonPropertyName("m")]
        public string PartitionKeyLowerBound { get; set; }

        [JsonPropertyName("u")]
        public string PartitionKeyUpperBound { get; set; }

        [JsonPropertyName("e")]
        public bool ExpandPartitionKeys { get; set; }

        [JsonPropertyName("v")]
        public JsonElement? ScanParameters { get; set; }

        [JsonPropertyName("d")]
        public JsonElement? DriverParameters { get; set; }
    }
}
