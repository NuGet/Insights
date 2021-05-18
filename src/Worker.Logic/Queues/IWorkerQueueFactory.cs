// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Azure.Storage.Queues;

namespace NuGet.Insights.Worker
{
    public interface IWorkerQueueFactory
    {
        Task InitializeAsync();
        Task<QueueClient> GetQueueAsync(QueueType type);
        Task<QueueClient> GetPoisonQueueAsync(QueueType type);
    }
}