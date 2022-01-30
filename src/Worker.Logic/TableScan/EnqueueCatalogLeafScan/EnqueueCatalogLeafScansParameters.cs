// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.EnqueueCatalogLeafScan
{
    public class EnqueueCatalogLeafScansParameters
    {
        [JsonPropertyName("o")]
        public bool OneMessagePerId { get; set; }
    }
}
