// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure.Storage.Blobs.Models;

namespace NuGet.Insights.Worker
{
    public interface ICsvRecordChunk<T> where T : ICsvRecord<T>
    {
        IReadOnlyList<T> GetRecords();
        string Position { get; }
    }

    public interface ICsvRecordProvider<T> where T : ICsvRecord<T>
    {
        bool ShouldCompact(BlobProperties? properties, ILogger logger);
        bool UseExistingRecords { get; }
        bool WriteEmptyCsv { get; }
        IAsyncEnumerable<ICsvRecordChunk<T>> GetChunksAsync(int bucket);
        Task<int> CountRemainingChunksAsync(int bucket, string? lastPosition);
        List<T> Prune(List<T> records, bool isFinalPrune, IOptions<NuGetInsightsWorkerSettings> options, ILogger logger);
        void AddBlobMetadata(Dictionary<string, string> metadata);
    }
}
