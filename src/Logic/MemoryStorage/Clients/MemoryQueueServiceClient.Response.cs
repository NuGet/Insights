// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public partial class MemoryQueueServiceClient
    {
        private IEnumerable<Page<QueueItem>> GetQueuePages(QueueTraits traits, string? prefix)
        {
            const int maxPerPageValue = StorageUtility.MaxTakeCount;
            return _store
                .GetQueueItems(traits, prefix)
                .Chunk(maxPerPageValue)
                .Select((x, i) => Page<QueueItem>.FromValues(
                    x,
                    continuationToken: x.Length == maxPerPageValue ? $"queue-item-page-{i}" : null,
                    new MemoryResponse(HttpStatusCode.OK)));
        }
    }
}
