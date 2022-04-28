// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface ITableScanDriver<T>
    {
        IList<string> SelectColumns { get; }
        Task InitializeAsync(JsonElement? parameters);
        Task ProcessEntitySegmentAsync(string tableName, JsonElement? parameters, IReadOnlyList<T> entities);
    }
}
