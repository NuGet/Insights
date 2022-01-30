// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IGenericMessageProcessor
    {
        Task ProcessSingleAsync(QueueType queue, ReadOnlyMemory<byte> message, long dequeueCount);
        Task ProcessBatchAsync(string schemaName, int schemaVersion, IReadOnlyList<JsonElement> data, long dequeueCount);
    }
}
