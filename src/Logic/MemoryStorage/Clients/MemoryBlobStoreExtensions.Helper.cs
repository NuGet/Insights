// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage;
using Azure.Storage.Blobs.Models;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public static partial class MemoryBlobStoreExtensions
    {
        public static StorageResult<BlobDownloadDetails> DownloadTo(
            this MemoryBlobStore store,
            Stream destination,
            BlobDownloadOptions? options,
            StorageTransferOptions transferOptions)
        {
            var result = store.DownloadStreaming(options, transferOptions);
            if (result.Type != StorageResultType.Success)
            {
                return new(result.Type);
            }

            result.Value.Content.CopyTo(destination);
            return new(StorageResultType.Success, result.Value.Details);
        }
    }
}
