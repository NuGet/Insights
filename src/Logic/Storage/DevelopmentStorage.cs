// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;

#nullable enable

namespace NuGet.Insights
{
    public static class DevelopmentStorage
    {
        public static StorageSharedKeyCredential StorageSharedKeyCredential { get; }
        public static TableSharedKeyCredential TableSharedKeyCredential { get; }
        public static Uri BlobEndpoint { get; } = new Uri("http://127.0.0.1:10000/devstoreaccount1");
        public static Uri QueueEndpoint { get; } = new Uri("http://127.0.0.1:10001/devstoreaccount1");
        public static Uri TableEndpoint { get; } = new Uri("http://127.0.0.1:10002/devstoreaccount1");

        public static (Uri Blob, Uri Queue, Uri Table) GetStorageEndpoints()
        {
            return (BlobEndpoint, QueueEndpoint, TableEndpoint);
        }

        static DevelopmentStorage()
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetProperty;

            var developmentStorageBlob = new BlobServiceClient("UseDevelopmentStorage=true");

            const string clientConfigurationName = "ClientConfiguration";
            var clientConfiguration = developmentStorageBlob
                .GetType()
                .GetProperty(clientConfigurationName, bindingFlags)?
                .GetValue(developmentStorageBlob);
            if (clientConfiguration is null)
            {
                throw new NotSupportedException($"Unable to find the {clientConfigurationName} on the {developmentStorageBlob.GetType()} type.");
            }

            const string sharedKeyCredentialName = "SharedKeyCredential";
            var storageSharedKeyCredential = (StorageSharedKeyCredential?)clientConfiguration
                .GetType()
                .GetProperty(sharedKeyCredentialName, bindingFlags)?
                .GetValue(clientConfiguration);
            if (storageSharedKeyCredential is null)
            {
                throw new NotSupportedException($"Unable to find the {sharedKeyCredentialName} on the {clientConfiguration.GetType()} type.");
            }

            var developmentStorageTable = new TableServiceClient("UseDevelopmentStorage=true");

            var tableSharedKeyCredential = (TableSharedKeyCredential?)developmentStorageTable
                .GetType()
                .GetProperty(sharedKeyCredentialName, bindingFlags)?
                .GetValue(developmentStorageTable);
            if (tableSharedKeyCredential is null)
            {
                throw new NotSupportedException($"Unable to find the {sharedKeyCredentialName} on the {developmentStorageTable.GetType()} type.");
            }

            StorageSharedKeyCredential = storageSharedKeyCredential;
            TableSharedKeyCredential = tableSharedKeyCredential;
        }
    }
}
