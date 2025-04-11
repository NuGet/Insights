// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Azure.Storage.Blobs.Models;

namespace NuGet.Insights.MemoryStorage
{
    public class MemoryBlobServiceStore
    {
        public static MemoryBlobServiceStore SharedStore { get; } = new(TimeProvider.System);

        private readonly ConcurrentDictionary<string, MemoryBlobContainerStore> _containers = new();

        private readonly TimeProvider _timeProvider;

        public MemoryBlobServiceStore(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public virtual IEnumerable<BlobContainerItem> GetContainerItems(
            BlobContainerTraits traits,
            BlobContainerStates states,
            string? prefix)
        {
            if (traits != BlobContainerTraits.None)
            {
                throw new NotImplementedException();
            }

            if (states != BlobContainerStates.None)
            {
                throw new NotImplementedException();
            }

            return _containers
                .Where(x => prefix == null || x.Key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(x => x.Value.GetBlobContainerItem())
                .Where(x => x.Type switch
                {
                    StorageResultType.Success => true,
                    StorageResultType.DoesNotExist => false,
                    _ => throw new NotImplementedException("Unexpected result type: " + x.Type),
                })
                .Select(x => x.Value)
                .OrderBy(x => x.Name, StringComparer.Ordinal);
        }

        public virtual MemoryBlobContainerStore GetContainer(string name)
        {
            return _containers.GetOrAdd(name, x => new MemoryBlobContainerStore(_timeProvider, x));
        }
    }
}
