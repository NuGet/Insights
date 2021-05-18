// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker
{
    public class TaskStateKey
    {
        [JsonConstructor]
        public TaskStateKey(string storageSuffix, string partitionKey, string rowKey)
        {
            StorageSuffix = storageSuffix;
            PartitionKey = partitionKey;
            RowKey = rowKey;
        }

        [JsonProperty("s")]
        public string StorageSuffix { get; }

        [JsonProperty("p")]
        public string PartitionKey { get; }

        [JsonProperty("r")]
        public string RowKey { get; }

        public TaskStateKey WithRowKeySuffix(string rowKeySuffix)
        {
            return new TaskStateKey(StorageSuffix, PartitionKey, RowKey + rowKeySuffix);
        }
    }
}
