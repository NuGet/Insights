// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Azure.Data.Tables;
using Newtonsoft.Json;

namespace NuGet.Insights.Worker.TableCopy
{
    public class TableRowCopyMessage<T> where T : class, ITableEntity, new()
    {
        [JsonProperty("s")]
        public string SourceTableName { get; set; }

        [JsonProperty("d")]
        public string DestinationTableName { get; set; }

        [JsonProperty("p")]
        public string PartitionKey { get; set; }

        [JsonProperty("r")]
        public List<string> RowKeys { get; set; }
    }
}
