// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights.Worker
{
    public interface ICsvRecordProvider<T> where T : ICsvRecord<T>
    {
        IAsyncEnumerable<ICsvRecordChunk<T>> GetChunksAsync(int bucket);
        Task<int> CountRemainingChunksAsync(int bucket, string? lastPosition);
        List<T> Prune(List<T> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger);
    }
}
