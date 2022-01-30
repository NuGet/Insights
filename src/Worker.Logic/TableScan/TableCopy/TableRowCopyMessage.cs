// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.TableCopy
{
    public class TableRowCopyMessage<T> where T : class, ITableEntity, new()
    {
        [JsonPropertyName("s")]
        public string SourceTableName { get; set; }

        [JsonPropertyName("d")]
        public string DestinationTableName { get; set; }

        [JsonPropertyName("p")]
        public string PartitionKey { get; set; }

        [JsonPropertyName("r")]
        public List<string> RowKeys { get; set; }
    }
}
