// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IMessageEnqueuer
    {
        Task EnqueueAsync<T>(IReadOnlyList<T> messages);
        Task EnqueueAsync<T>(IReadOnlyList<T> messages, Func<T, IReadOnlyList<T>> split);
        Task EnqueueAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore);
        Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages);
        Task EnqueuePoisonAsync<T>(IReadOnlyList<T> messages, TimeSpan notBefore);
        Task EnqueueAsync<T>(QueueType queue, bool isPoison, IReadOnlyList<T> messages, Func<T, IReadOnlyList<T>> split, TimeSpan notBefore);
        QueueType GetQueueType<T>();
        QueueType GetQueueType(string schemaName);
        Task InitializeAsync();
    }
}
