// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class ContainerInitializationState
    {
        private readonly Func<Task> _initializeAsync;
        private readonly Func<Task>? _destroyAsync;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private bool _created;
        private DateTimeOffset _createdAt;

        private ContainerInitializationState(
            Func<Task> initializeAsync,
            Func<Task>? destroyAsync)
        {
            _initializeAsync = initializeAsync;
            _destroyAsync = destroyAsync;
        }

        public async Task InitializeAsync()
        {
            if (!ShouldInitialize())
            {
                return;
            }

            await _semaphore.WaitAsync();
            try
            {
                if (!ShouldInitialize())
                {
                    return;
                }

                await _initializeAsync();
                _createdAt = DateTimeOffset.UtcNow;
                _created = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task DestroyAsync()
        {
            if (_destroyAsync is null)
            {
                throw new NotSupportedException();
            }

            await _semaphore.WaitAsync();
            try
            {
                _created = false;
                await _destroyAsync();
                await _initializeAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private bool ShouldInitialize()
        {
            if (!_created)
            {
                return true;
            }

            var sinceCreated = DateTimeOffset.UtcNow - _createdAt;
            return sinceCreated > TimeSpan.FromMinutes(15);
        }

        public static ContainerInitializationState New(Func<Task> initializeAsync)
        {
            return new ContainerInitializationState(initializeAsync, destroyAsync: null);
        }

        public static ContainerInitializationState New(Func<Task> initializeAsync, Func<Task> destroyAsync)
        {
            return new ContainerInitializationState(initializeAsync, destroyAsync);
        }

        public static ContainerInitializationState BlobContainer(ServiceClientFactory serviceClientFactory, string containerName)
        {
            return new ContainerInitializationState(
                initializeAsync: async () =>
                {
                    var serviceClient = await serviceClientFactory.GetBlobServiceClientAsync();
                    var container = serviceClient.GetBlobContainerClient(containerName);
                    await container.CreateIfNotExistsAsync(retry: true);
                },
                destroyAsync: async () =>
                {
                    var serviceClient = await serviceClientFactory.GetBlobServiceClientAsync();
                    var container = serviceClient.GetBlobContainerClient(containerName);
                    await container.DeleteIfExistsAsync();
                });
        }

        public static ContainerInitializationState Table(ServiceClientFactory serviceClientFactory, string tableName)
        {
            return new ContainerInitializationState(
                initializeAsync: async () =>
                {
                    var serviceClient = await serviceClientFactory.GetTableServiceClientAsync();
                    var table = serviceClient.GetTableClient(tableName);
                    await table.CreateIfNotExistsAsync(retry: true);
                },
                destroyAsync: async () =>
                {
                    var serviceClient = await serviceClientFactory.GetTableServiceClientAsync();
                    var table = serviceClient.GetTableClient(tableName);
                    await table.DeleteAsync();
                });
        }

        public static ContainerInitializationState Queue(ServiceClientFactory serviceClientFactory, string queueName)
        {
            return new ContainerInitializationState(
                initializeAsync: async () =>
                {
                    var serviceClient = await serviceClientFactory.GetQueueServiceClientAsync();
                    var queue = serviceClient.GetQueueClient(queueName);
                    await queue.CreateIfNotExistsAsync(retry: true);
                },
                destroyAsync: async () =>
                {
                    var serviceClient = await serviceClientFactory.GetQueueServiceClientAsync();
                    var queue = serviceClient.GetQueueClient(queueName);
                    await queue.DeleteIfExistsAsync();
                });
        }
    }
}
