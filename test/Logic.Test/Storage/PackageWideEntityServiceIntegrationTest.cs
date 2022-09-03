// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class PackageWideEntityServiceIntegrationTest : BaseLogicIntegrationTest
    {
        public class TheUpdateBatchAsyncMethod : PackageWideEntityServiceIntegrationTest
        {
            [Fact]
            public async Task FetchesDataForNewItem()
            {
                await Target.InitializeAsync(TableName);

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task FetchesDataForUpdatedItem()
            {
                await Target.InitializeAsync(TableName);
                await UpdateBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp += TimeSpan.FromDays(1);

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(CommitTimestamp.AddDays(1), pair.Value.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task DoesNotFetchDataForOldItem()
            {
                await Target.InitializeAsync(TableName);
                await UpdateBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp -= TimeSpan.FromDays(1);

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp.Value.AddDays(1), pair.Value.CommitTimestamp);
                Assert.False(DataFetched);
            }

            [Fact]
            public async Task FetchesDataForExistingItemWhenInputHasNoCommitTimestamp()
            {
                await Target.InitializeAsync(TableName);
                await UpdateBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp = null;

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.Null(pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task FetchesDataForExistingItemWhenExistingHasNoCommitTimestamp()
            {
                await Target.InitializeAsync(TableName);
                Item.CommitTimestamp = null;
                await UpdateBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp = CommitTimestamp;

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task FetchesDataForExistingItemWhenInputAndExistingHasNoCommitTimestamp()
            {
                await Target.InitializeAsync(TableName);
                Item.CommitTimestamp = null;
                await UpdateBatchAsync();
                DataFetched = false;

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.Null(pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task NullableModelWithNonNullValueCanOperateOnNonNullableData()
            {
                await Target.InitializeAsync(TableName);
                await UpdateNonNullBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp += TimeSpan.FromDays(1);

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(CommitTimestamp.AddDays(1), pair.Value.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task NullableModelWithNullValueCanOperateOnNonNullableData()
            {
                await Target.InitializeAsync(TableName);
                await UpdateNonNullBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp = null;

                var output = await UpdateBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.Null(pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task NonNullableModelCanOperateOnNullableDataWithNonNullValue()
            {
                await Target.InitializeAsync(TableName);
                await UpdateBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp += TimeSpan.FromDays(1);

                var output = await UpdateNonNullBatchAsync();

                var pair = Assert.Single(output);
                Assert.Same(Item, pair.Key);
                Assert.Equal(CommitTimestamp.AddDays(1), pair.Value.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp, pair.Value.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task NonNullableModelCannotOperateOnNullableDataWithNullValue()
            {
                await Target.InitializeAsync(TableName);
                Item.CommitTimestamp = null;
                await UpdateBatchAsync();
                DataFetched = false;
                Item.CommitTimestamp = CommitTimestamp;

                var ex = await Assert.ThrowsAsync<MessagePackSerializationException>(() => UpdateNonNullBatchAsync());
                Assert.Contains("Unexpected msgpack code 192 (nil) encountered.", ex.ToString());
                Assert.False(DataFetched);
            }

            public TheUpdateBatchAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            protected async Task<IReadOnlyDictionary<IPackageIdentityCommit, TestPackageWideEntityV1>> UpdateBatchAsync()
            {
                return await Target.UpdateBatchAsync(
                    TableName,
                    Item.PackageId,
                    new[] { (IPackageIdentityCommit)Item },
                    x =>
                    {
                        DataFetched = true;
                        return Task.FromResult(new TestPackageWideEntityV1 { CommitTimestamp = x.CommitTimestamp });
                    },
                    x => new TestPackageWideEntityVersions(x),
                    x => x.V1);
            }

            protected async Task<IReadOnlyDictionary<IPackageIdentityCommit, NonNullTestPackageWideEntityV1>> UpdateNonNullBatchAsync()
            {
                return await Target.UpdateBatchAsync(
                    TableName,
                    Item.PackageId,
                    new[] { (IPackageIdentityCommit)Item },
                    x =>
                    {
                        DataFetched = true;
                        return Task.FromResult(new NonNullTestPackageWideEntityV1 { CommitTimestamp = x.CommitTimestamp.Value });
                    },
                    x => new NonNullTestPackageWideEntityVersions(x),
                    x => x.V1);
            }
        }

        public class TheGetOrUpdateInfoAsyncMethod : PackageWideEntityServiceIntegrationTest
        {
            [Fact]
            public async Task FetchesDataForNewItem()
            {
                await Target.InitializeAsync(TableName);

                var output = await GetOrUpdateInfoAsync();

                Assert.Equal(Item.CommitTimestamp, output.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task FetchesDataForUpdatedItem()
            {
                await Target.InitializeAsync(TableName);
                await GetOrUpdateInfoAsync();
                DataFetched = false;
                Item.CommitTimestamp += TimeSpan.FromDays(1);

                var output = await GetOrUpdateInfoAsync();

                Assert.Equal(CommitTimestamp.AddDays(1), output.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp, output.CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task DoesNotFetchDataForOldItem()
            {
                await Target.InitializeAsync(TableName);
                await GetOrUpdateInfoAsync();
                DataFetched = false;
                Item.CommitTimestamp -= TimeSpan.FromDays(1);

                var output = await GetOrUpdateInfoAsync();

                Assert.Equal(CommitTimestamp, output.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp.Value.AddDays(1), output.CommitTimestamp);
                Assert.False(DataFetched);
            }

            [Fact]
            public async Task DoesNotFetchDataForExistingItemWhenInputHasNoCommitTimestamp()
            {
                await Target.InitializeAsync(TableName);
                await GetOrUpdateInfoAsync();
                DataFetched = false;
                Item.CommitTimestamp = null;

                var output = await GetOrUpdateInfoAsync();

                Assert.Equal(CommitTimestamp, output.CommitTimestamp);
                Assert.Null(Item.CommitTimestamp);
                Assert.False(DataFetched);
            }

            [Fact]
            public async Task FetchesDataForExistingItemWhenExistingHasNoCommitTimestamp()
            {
                await Target.InitializeAsync(TableName);
                Item.CommitTimestamp = null;
                await GetOrUpdateInfoAsync();
                DataFetched = false;
                Item.CommitTimestamp = CommitTimestamp;

                var output = await GetOrUpdateInfoAsync();

                Assert.Equal(CommitTimestamp, output.CommitTimestamp);
                Assert.Equal(Item.CommitTimestamp, CommitTimestamp);
                Assert.True(DataFetched);
            }

            [Fact]
            public async Task DoesNotFetchDataForExistingItemWhenInputAndExistingHasNoCommitTimestamp()
            {
                await Target.InitializeAsync(TableName);
                Item.CommitTimestamp = null;
                await GetOrUpdateInfoAsync();
                DataFetched = false;

                var output = await GetOrUpdateInfoAsync();

                Assert.Equal(Item.CommitTimestamp, output.CommitTimestamp);
                Assert.Null(output.CommitTimestamp);
                Assert.False(DataFetched);
            }

            public TheGetOrUpdateInfoAsyncMethod(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }

            protected async Task<TestPackageWideEntityV1> GetOrUpdateInfoAsync()
            {
                return await Target.GetOrUpdateInfoAsync(
                    TableName,
                    Item,
                    x =>
                    {
                        DataFetched = true;
                        return Task.FromResult(new TestPackageWideEntityV1 { CommitTimestamp = x.CommitTimestamp });
                    },
                    x => new TestPackageWideEntityVersions(x),
                    x => x.V1);
            }
        }

        public PackageWideEntityServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            TableName = TestSettings.NewStoragePrefix() + "1pwes1";
            CommitTimestamp = new DateTimeOffset(2022, 6, 25, 10, 45, 0, TimeSpan.Zero);
            Item = new PackageIdentityWithoutCommit("NuGet.Versioning", "6.0.0", CommitTimestamp);
        }

        public PackageWideEntityService Target => Host.Services.GetRequiredService<PackageWideEntityService>();
        public string TableName { get; }
        public DateTimeOffset CommitTimestamp { get; set; }
        public PackageIdentityWithoutCommit Item { get; }
        public bool DataFetched { get; set; }

        public class PackageIdentityWithoutCommit : IPackageIdentityCommit
        {
            public PackageIdentityWithoutCommit(string id, string version, DateTimeOffset? commitTimestamp)
            {
                PackageId = id;
                PackageVersion = version;
                CommitTimestamp = commitTimestamp;
            }

            public string PackageId { get; set; }

            public string PackageVersion { get; set; }

            public DateTimeOffset? CommitTimestamp { get; set; }
        }

        [MessagePackObject]
        public class TestPackageWideEntityVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public TestPackageWideEntityVersions(TestPackageWideEntityV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public TestPackageWideEntityV1 V1 { get; set; }

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class TestPackageWideEntityV1
        {
            [Key(1)]
            public DateTimeOffset? CommitTimestamp { get; set; }
        }

        [MessagePackObject]
        public class NonNullTestPackageWideEntityVersions : PackageWideEntityService.IPackageWideEntity
        {
            [SerializationConstructor]
            public NonNullTestPackageWideEntityVersions(NonNullTestPackageWideEntityV1 v1)
            {
                V1 = v1;
            }

            [Key(0)]
            public NonNullTestPackageWideEntityV1 V1 { get; set; }

            DateTimeOffset? PackageWideEntityService.IPackageWideEntity.CommitTimestamp => V1.CommitTimestamp;
        }

        [MessagePackObject]
        public class NonNullTestPackageWideEntityV1
        {
            [Key(1)]
            public DateTimeOffset CommitTimestamp { get; set; }
        }
    }
}
