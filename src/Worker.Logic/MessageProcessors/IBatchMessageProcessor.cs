// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGet.Insights.Worker
{
    public interface IBatchMessageProcessor<T>
    {
        Task<BatchMessageProcessorResult<T>> ProcessAsync(IReadOnlyList<T> messages, long dequeueCount);
    }
}
