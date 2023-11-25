// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public interface ITaskStateMessageProcessor<T> where T : ITaskStateMessage
    {
        Task<TaskStateProcessResult> ProcessAsync(T message, TaskState taskState, long dequeueCount);
    }
}
