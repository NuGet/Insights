// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace NuGet.Insights.Worker
{
    public interface IGenericMessageProcessor
    {
        Task ProcessSingleAsync(QueueType queue, string message, long dequeueCount);
        Task ProcessBatchAsync(string schemaName, int schemaVersion, IReadOnlyList<JToken> data, long dequeueCount);
    }
}