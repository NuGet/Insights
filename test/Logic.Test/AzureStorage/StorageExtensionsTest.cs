// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class StorageExtensionsTest : BaseLogicIntegrationTest
    {
        public class Blobs : StorageExtensionsTest
        {
            public Blobs(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_ContainerDoesNotExist()
            {
                var container = GetContainer("b");
                Assert.False(await container.ExistsAsync());
                await container.CreateIfNotExistsAsync(retry: true);
                Assert.True(await container.ExistsAsync());
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_ContainerDoesExist()
            {
                var container = GetContainer("b");
                await container.CreateIfNotExistsAsync();
                Assert.True(await container.ExistsAsync());
                await container.CreateIfNotExistsAsync(retry: true);
                Assert.True(await container.ExistsAsync());
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_ContainerWasJustDeleted()
            {
                var container = GetContainer("b");
                await container.CreateIfNotExistsAsync();
                await container.DeleteAsync();
                Assert.False(await container.ExistsAsync());
                await container.CreateIfNotExistsAsync(retry: true);
                Assert.True(await container.ExistsAsync());
            }
        }

        public class Queues : StorageExtensionsTest
        {
            public Queues(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_QueueDoesNotExist()
            {
                var queue = GetQueue("b");
                Assert.False(await queue.ExistsAsync());
                await queue.CreateIfNotExistsAsync(retry: true);
                Assert.True(await queue.ExistsAsync());
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_QueueDoesExist()
            {
                var queue = GetQueue("b");
                await queue.CreateIfNotExistsAsync();
                Assert.True(await queue.ExistsAsync());
                await queue.CreateIfNotExistsAsync(retry: true);
                Assert.True(await queue.ExistsAsync());
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_QueueWasJustDeleted()
            {
                var queue = GetQueue("b");
                await queue.CreateIfNotExistsAsync();
                await queue.DeleteAsync();
                Assert.False(await queue.ExistsAsync());
                await queue.CreateIfNotExistsAsync(retry: true);
                Assert.True(await queue.ExistsAsync());
            }
        }

        public class Tables : StorageExtensionsTest
        {
            public Tables(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_TableDoesNotExist()
            {
                var table = GetTable("b");
                Assert.False(await table.ExistsAsync());
                await table.CreateIfNotExistsAsync(retry: true);
                Assert.True(await table.ExistsAsync());
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_TableDoesExist()
            {
                var table = GetTable("b");
                await table.CreateIfNotExistsAsync();
                Assert.True(await table.ExistsAsync());
                await table.CreateIfNotExistsAsync(retry: true);
                Assert.True(await table.ExistsAsync());
            }

            [Fact]
            public async Task CreateIfNotExistsAsync_TableWasJustDeleted()
            {
                var table = GetTable("b");
                await table.CreateIfNotExistsAsync();
                await table.DeleteAsync();
                Assert.False(await table.ExistsAsync());
                await table.CreateIfNotExistsAsync(retry: true);
                Assert.True(await table.ExistsAsync());
            }
        }

        public BlobServiceClient BlobServiceClient { get; private set; }
        public QueueServiceClient QueueServiceClient { get; private set; }
        public TableServiceClientWithRetryContext TableServiceClient { get; private set; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            BlobServiceClient = await ServiceClientFactory.GetBlobServiceClientAsync();
            QueueServiceClient = await ServiceClientFactory.GetQueueServiceClientAsync();
            TableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
        }

        private BlobContainerClient GetContainer(string nameSuffix)
        {
            return BlobServiceClient.GetBlobContainerClient(StoragePrefix + nameSuffix);
        }

        private QueueClient GetQueue(string nameSuffix)
        {
            return QueueServiceClient.GetQueueClient(StoragePrefix + nameSuffix);
        }

        private TableClientWithRetryContext GetTable(string nameSuffix)
        {
            return TableServiceClient.GetTableClient(StoragePrefix + nameSuffix);
        }

        public StorageExtensionsTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
