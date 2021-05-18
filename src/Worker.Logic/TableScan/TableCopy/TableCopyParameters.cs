// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace NuGet.Insights.Worker.TableCopy
{
    public class TableCopyParameters
    {
        [JsonProperty("d")]
        public string DestinationTableName { get; set; }
    }
}
