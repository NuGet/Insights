// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Queues.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryQueueServiceStore
    {
        public static MemoryQueueServiceStore SharedStore { get; } = new(TimeProvider.System);

        private readonly ConcurrentDictionary<string, MemoryQueueStore> _queues = new();

        private readonly TimeProvider _timeProvider;

        public MemoryQueueServiceStore(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public virtual IEnumerable<QueueItem> GetQueueItems(QueueTraits traits, string? prefix)
        {
            return _queues
                .Where(x => prefix == null || x.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(x => x.Value.GetQueueItem(traits))
                .Where(x => x.Type switch
                {
                    StorageResultType.Success => true,
                    StorageResultType.DoesNotExist => false,
                    _ => throw new NotImplementedException("Unexpected result type: " + x.Type),
                })
                .Select(x => x.Value)
                .OrderBy(x => x.Name, StringComparer.Ordinal);
        }

        public virtual MemoryQueueStore GetQueue(string name)
        {
            return _queues.GetOrAdd(name, x => new MemoryQueueStore(_timeProvider, x));
        }
    }
}
