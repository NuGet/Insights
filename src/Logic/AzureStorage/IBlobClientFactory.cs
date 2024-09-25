// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using NuGet.Insights.MemoryStorage;

#nullable enable

namespace NuGet.Insights
{
    public interface IBlobClientFactory
    {
        BlobServiceClient GetServiceClient();
        bool TryGetBlobClient(BlobServiceClient serviceClient, Uri blobUri, [NotNullWhen(true)] out BlobClient? blobClient);
    }

    public class MemoryBlobClientFactory : IBlobClientFactory
    {
        public static MemoryBlobServiceStore SharedStore { get; } = new();

        public BlobServiceClient GetServiceClient() => new MemoryBlobServiceClient(SharedStore);

        public bool TryGetBlobClient(BlobServiceClient serviceClient, Uri blobUri, [NotNullWhen(true)] out BlobClient? blobClient)
        {
            return StorageUtility.TryGetAccountBlobClient(
                serviceClient,
                blobUri,
                out blobClient);
        }
    }

    public class DevelopmentBlobClientFactory : IBlobClientFactory
    {
        private readonly BlobClientOptions _options;

        public DevelopmentBlobClientFactory(BlobClientOptions options)
        {
            _options = options;
        }

        public BlobServiceClient GetServiceClient() => new BlobServiceClient(StorageUtility.DevelopmentConnectionString, _options);

        public bool TryGetBlobClient(BlobServiceClient serviceClient, Uri blobUri, [NotNullWhen(true)] out BlobClient? blobClient)
        {
            return StorageUtility.TryGetAccountBlobClient(serviceClient, blobUri, out blobClient);
        }
    }

    public class TokenCredentialBlobClientFactory : IBlobClientFactory
    {
        private readonly Uri _serviceUri;
        private readonly TokenCredential _credential;
        private readonly BlobClientOptions _options;

        public TokenCredentialBlobClientFactory(Uri serviceUri, TokenCredential credential, BlobClientOptions options)
        {
            _serviceUri = serviceUri;
            _credential = credential;
            _options = options;
        }

        public BlobServiceClient GetServiceClient() => new BlobServiceClient(_serviceUri, _credential, _options);

        public bool TryGetBlobClient(BlobServiceClient serviceClient, Uri blobUri, [NotNullWhen(true)] out BlobClient? blobClient)
        {
            if (blobUri.Scheme != "https" // only allow HTTPS
                || !blobUri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase) // only allow blob storage URLs
                || !string.IsNullOrEmpty(blobUri.Query)) // don't allow SAS tokens
            {
                blobClient = null;
                return false;
            }

            blobClient = new BlobClient(blobUri, _credential, _options);
            return true;
        }
    }

    public class SharedKeyCredentialBlobClientFactory : IBlobClientFactory
    {
        private readonly Uri _serviceUri;
        private readonly StorageSharedKeyCredential _credential;
        private readonly BlobClientOptions _options;

        public SharedKeyCredentialBlobClientFactory(Uri serviceUri, StorageSharedKeyCredential credential, BlobClientOptions options)
        {
            _serviceUri = serviceUri;
            _credential = credential;
            _options = options;
        }

        public BlobServiceClient GetServiceClient() => new BlobServiceClient(_serviceUri, _credential, _options);

        public bool TryGetBlobClient(BlobServiceClient serviceClient, Uri blobUri, [NotNullWhen(true)] out BlobClient? blobClient)
        {
            return StorageUtility.TryGetAccountBlobClient(serviceClient, blobUri, out blobClient);
        }
    }
}
