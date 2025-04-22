// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class TableExtensionsTest : BaseLogicIntegrationTest
    {
        [Fact]
        public async Task ExistsAsync_FalseWhenNotCreated()
        {
            Assert.False(await TableServiceClient.TableExistsAsync(Table.Name));
        }

        [Fact]
        public async Task ExistsAsync_TrueWhenNotCreated()
        {
            await Table.CreateIfNotExistsAsync();

            Assert.True(await TableServiceClient.TableExistsAsync(Table.Name));
        }

        [Fact]
        public async Task ExistsAsync_FalseWhenJustDeleted()
        {
            await Table.CreateIfNotExistsAsync();
            await Table.DeleteAsync();

            Assert.False(await TableServiceClient.TableExistsAsync(Table.Name));
        }

        [Fact]
        public async Task GetEntityOrNullAsync_ReturnsNullWhenTableDoesNotExist()
        {
            Assert.Null(await Table.GetEntityOrNullAsync<TableEntity>("foo", "bar"));
        }

        [Fact]
        public async Task GetEntityOrNullAsync_ReturnsNullWhenEntityDoesNotExist()
        {
            await Table.CreateIfNotExistsAsync();

            Assert.Null(await Table.GetEntityOrNullAsync<TableEntity>("foo", "bar"));
        }

        [Fact]
        public async Task GetEntityOrNullAsync_ReturnsEntityWhenExists()
        {
            await Table.CreateIfNotExistsAsync();
            await Table.AddEntityAsync(new TableEntity("foo", "bar"));

            Assert.NotNull(await Table.GetEntityOrNullAsync<TableEntity>("foo", "bar"));
        }

        [Fact]
        public async Task QueryAsync_ReturnsTablesWithMatchingPrefix()
        {
            await GetTable("a").CreateIfNotExistsAsync();
            await GetTable("aa").CreateIfNotExistsAsync();
            await GetTable("b").CreateIfNotExistsAsync();
            await GetTable("ba").CreateIfNotExistsAsync();
            await GetTable("bba").CreateIfNotExistsAsync();
            await GetTable("bbb").CreateIfNotExistsAsync();
            await GetTable("bcc").CreateIfNotExistsAsync();
            await GetTable("c").CreateIfNotExistsAsync();
            await GetTable("cb").CreateIfNotExistsAsync();

            var tables = await TableServiceClient.QueryAsync(prefix: StoragePrefix + "b").ToListAsync();

            var tableNames = tables.Select(x => x.Name.Replace(StoragePrefix, string.Empty, StringComparison.Ordinal)).ToList();
            Assert.Equal(["b", "ba", "bba", "bbb", "bcc"], tableNames);
        }

        public TableServiceClientWithRetryContext TableServiceClient { get; private set; }
        public TableClientWithRetryContext Table { get; private set; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();

            TableServiceClient = await ServiceClientFactory.GetTableServiceClientAsync();
            Table = GetTable("1t1");
        }

        private TableClientWithRetryContext GetTable(string nameSuffix)
        {
            return TableServiceClient.GetTableClient(StoragePrefix + nameSuffix);
        }

        public TableExtensionsTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
