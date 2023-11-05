// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public class MutableTableTransactionalBatchTest : IClassFixture<MutableTableTransactionalBatchTest.Fixture>
    {
        public class TheAddEntityMethod : MutableTableTransactionalBatchTest
        {
            [Fact]
            public async Task AddsSingleEntity()
            {
                var entity = new TestEntity(PartitionKey, "a");
                var target = await GetTargetAsync();
                var initialETag = entity.ETag;

                target.AddEntity(entity);
                await target.SubmitBatchAsync();

                var output = Assert.Single(await GetEntitiesAsync());
                Assert.Equal(entity.ETag, output.ETag);
                Assert.NotEqual(initialETag, entity.ETag);
            }

            [Fact]
            public async Task AddsMultipleEntities()
            {
                var entityA = new TestEntity(PartitionKey, "a");
                var entityB = new TestEntity(PartitionKey, "b");
                var target = await GetTargetAsync();
                var initialETagA = entityA.ETag;
                var initialETagB = entityB.ETag;

                target.AddEntity(entityA);
                target.AddEntity(entityB);
                await target.SubmitBatchAsync();

                var entities = await GetEntitiesAsync();
                Assert.Equal(2, entities.Count);
                Assert.Equal(entityA.ETag, entities[0].ETag);
                Assert.Equal(entityB.ETag, entities[1].ETag);
                Assert.NotEqual(initialETagA, entityA.ETag);
                Assert.NotEqual(initialETagB, entityB.ETag);
            }


            [Fact]
            public async Task OperatesAsTransaction()
            {
                await AddEntityAsync("b");
                var target = await GetTargetAsync();

                target.AddEntity(new TestEntity(PartitionKey, "a"));
                target.AddEntity(new TestEntity(PartitionKey, "b"));
                target.AddEntity(new TestEntity(PartitionKey, "c"));
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => target.SubmitBatchAsync());

                Assert.Equal((int)HttpStatusCode.Conflict, ex.Status);
                var entity = Assert.Single(await GetEntitiesAsync());
                Assert.Equal("b", entity.RowKey);
            }

            public TheAddEntityMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheDeleteEntityMethod : MutableTableTransactionalBatchTest
        {
            [Fact]
            public async Task DeletesSingleEntity()
            {
                var entity = await AddEntityAsync("a");
                var target = await GetTargetAsync();

                target.DeleteEntity(entity.PartitionKey, entity.RowKey, entity.ETag);
                await target.SubmitBatchAsync();

                Assert.Empty(await GetEntitiesAsync());
            }

            [Fact]
            public async Task DeletesMultipleEntities()
            {
                var entityA = await AddEntityAsync("a");
                await AddEntityAsync("b");
                var entityC = await AddEntityAsync("c");
                var target = await GetTargetAsync();

                target.DeleteEntity(entityA.PartitionKey, entityA.RowKey, entityA.ETag);
                target.DeleteEntity(entityC.PartitionKey, entityC.RowKey, entityC.ETag);
                await target.SubmitBatchAsync();

                var entity = Assert.Single(await GetEntitiesAsync());
                Assert.Equal("b", entity.RowKey);
            }

            [Fact]
            public async Task OperatesAsTransaction()
            {
                var entityA = await AddEntityAsync("a");
                var entityB = await AddEntityAsync("b");
                var target = await GetTargetAsync();

                target.DeleteEntity(entityA.PartitionKey, entityA.RowKey, entityA.ETag);
                target.DeleteEntity(entityB.PartitionKey, entityB.RowKey, new ETag("W/\"wrong\""));
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => target.SubmitBatchAsync());

                Assert.Equal((int)HttpStatusCode.PreconditionFailed, ex.Status);
                var entities = await GetEntitiesAsync();
                Assert.Equal(2, entities.Count);
            }

            [Fact]
            public async Task FailsDeletingSingleNonExistentEntity()
            {
                var target = await GetTargetAsync();

                target.DeleteEntity(PartitionKey, "a", ETag.All);
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => target.SubmitBatchAsync());

                Assert.Equal((int)HttpStatusCode.NotFound, ex.Status);
            }

            [Fact]
            public async Task FailsDeletingMultipleNonExistentEntities()
            {
                var target = await GetTargetAsync();

                target.DeleteEntity(PartitionKey, "a", ETag.All);
                target.DeleteEntity(PartitionKey, "b", ETag.All);
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => target.SubmitBatchAsync());

                Assert.Equal((int)HttpStatusCode.NotFound, ex.Status);
            }

            public TheDeleteEntityMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheUpdateMergeEntityMethod : MutableTableTransactionalBatchTest
        {
            [Fact]
            public async Task MergesSingleEntity()
            {
                var entity = await AddEntityAsync("a", fieldA: "foo");
                var target = await GetTargetAsync();

                entity.FieldA = null;
                entity.FieldB = "bar";
                target.UpdateEntity(entity, entity.ETag, TableUpdateMode.Merge);
                await target.SubmitBatchAsync();

                var output = Assert.Single(await GetEntitiesAsync());
                Assert.Equal("foo", output.FieldA);
                Assert.Equal("bar", output.FieldB);
            }

            [Fact]
            public async Task MergesMultipleEntities()
            {
                var entityA = await AddEntityAsync("a", fieldA: "foo");
                await AddEntityAsync("b");
                var entityC = await AddEntityAsync("c", fieldB: "baz");
                var target = await GetTargetAsync();

                entityA.FieldA = null;
                entityA.FieldB = "bar";
                entityC.FieldA = null;
                entityC.FieldB = "qux";
                target.UpdateEntity(entityA, entityA.ETag, TableUpdateMode.Merge);
                target.UpdateEntity(entityC, entityC.ETag, TableUpdateMode.Merge);
                await target.SubmitBatchAsync();

                var output = await GetEntitiesAsync();
                Assert.Equal(3, output.Count);
                Assert.Equal("foo", output[0].FieldA);
                Assert.Equal("bar", output[0].FieldB);
                Assert.Null(output[1].FieldA);
                Assert.Null(output[1].FieldB);
                Assert.Null(output[2].FieldA);
                Assert.Equal("qux", output[2].FieldB);
            }

            [Fact]
            public async Task OperatesAsTransaction()
            {
                var entityA = await AddEntityAsync("a");
                var entityB = await AddEntityAsync("b");
                var target = await GetTargetAsync();

                entityA.FieldA = "foo";
                entityB.FieldA = "bar";
                target.UpdateEntity(entityA, entityA.ETag, TableUpdateMode.Merge);
                target.UpdateEntity(entityB, new ETag("W/\"wrong\""), TableUpdateMode.Merge);
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => target.SubmitBatchAsync());

                Assert.Equal((int)HttpStatusCode.PreconditionFailed, ex.Status);
                var entities = await GetEntitiesAsync();
                Assert.Equal(2, entities.Count);
                Assert.Null(entities[0].FieldA);
                Assert.Null(entities[1].FieldA);
            }

            public TheUpdateMergeEntityMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheUpdateReplaceEntityMethod : MutableTableTransactionalBatchTest
        {
            [Fact]
            public async Task ReplacesSingleEntity()
            {
                var entity = await AddEntityAsync("a", fieldA: "foo");
                var target = await GetTargetAsync();

                entity.FieldA = null;
                entity.FieldB = "bar";
                target.UpdateEntity(entity, entity.ETag, TableUpdateMode.Replace);
                await target.SubmitBatchAsync();

                var output = Assert.Single(await GetEntitiesAsync());
                Assert.Null(output.FieldA);
                Assert.Equal("bar", output.FieldB);
            }

            [Fact]
            public async Task ReplacesMultipleEntities()
            {
                var entityA = await AddEntityAsync("a", fieldA: "foo");
                await AddEntityAsync("b");
                var entityC = await AddEntityAsync("c", fieldB: "baz");
                var target = await GetTargetAsync();

                entityA.FieldA = null;
                entityA.FieldB = "bar";
                entityC.FieldA = null;
                entityC.FieldB = "qux";
                target.UpdateEntity(entityA, entityA.ETag, TableUpdateMode.Replace);
                target.UpdateEntity(entityC, entityC.ETag, TableUpdateMode.Replace);
                await target.SubmitBatchAsync();

                var output = await GetEntitiesAsync();
                Assert.Equal(3, output.Count);
                Assert.Null(output[0].FieldA);
                Assert.Equal("bar", output[0].FieldB);
                Assert.Null(output[1].FieldA);
                Assert.Null(output[1].FieldB);
                Assert.Null(output[2].FieldA);
                Assert.Equal("qux", output[2].FieldB);
            }

            [Fact]
            public async Task OperatesAsTransaction()
            {
                var entityA = await AddEntityAsync("a");
                var entityB = await AddEntityAsync("b");
                var target = await GetTargetAsync();

                entityA.FieldA = "foo";
                entityB.FieldA = "bar";
                target.UpdateEntity(entityA, entityA.ETag, TableUpdateMode.Replace);
                target.UpdateEntity(entityB, new ETag("W/\"wrong\""), TableUpdateMode.Replace);
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => target.SubmitBatchAsync());

                Assert.Equal((int)HttpStatusCode.PreconditionFailed, ex.Status);
                var entities = await GetEntitiesAsync();
                Assert.Equal(2, entities.Count);
                Assert.Null(entities[0].FieldA);
                Assert.Null(entities[1].FieldA);
            }

            public TheUpdateReplaceEntityMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheUpsertMergeEntityMethod : MutableTableTransactionalBatchTest
        {
            [Fact]
            public async Task AddsSingleEntity()
            {
                var entity = new TestEntity(PartitionKey, "a");
                var target = await GetTargetAsync();

                target.UpsertEntity(entity, TableUpdateMode.Merge);
                await target.SubmitBatchAsync();

                Assert.Single(await GetEntitiesAsync());
            }

            [Fact]
            public async Task MergesSingleEntity()
            {
                var entity = await AddEntityAsync("a", fieldA: "foo");
                var target = await GetTargetAsync();

                entity.FieldA = null;
                entity.FieldB = "bar";
                target.UpsertEntity(entity, TableUpdateMode.Merge);
                await target.SubmitBatchAsync();

                var output = Assert.Single(await GetEntitiesAsync());
                Assert.Equal("foo", output.FieldA);
                Assert.Equal("bar", output.FieldB);
            }

            [Fact]
            public async Task MergesMultipleEntities()
            {
                var entityA = new TestEntity(PartitionKey, "a")
                {
                    FieldA = "foo",
                    FieldB = "bar",
                };
                await AddEntityAsync("b");
                var entityC = await AddEntityAsync("c", fieldB: "baz");
                var target = await GetTargetAsync();

                entityC.FieldA = "qux";
                entityC.FieldB = null;
                target.UpsertEntity(entityA, TableUpdateMode.Merge);
                target.UpsertEntity(entityC, TableUpdateMode.Merge);
                await target.SubmitBatchAsync();

                var output = await GetEntitiesAsync();
                Assert.Equal(3, output.Count);
                Assert.Equal("foo", output[0].FieldA);
                Assert.Equal("bar", output[0].FieldB);
                Assert.Null(output[1].FieldA);
                Assert.Null(output[1].FieldB);
                Assert.Equal("qux", output[2].FieldA);
                Assert.Equal("baz", output[2].FieldB);
            }

            public TheUpsertMergeEntityMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheUpsertReplaceEntityMethod : MutableTableTransactionalBatchTest
        {
            [Fact]
            public async Task AddsSingleEntity()
            {
                var entity = new TestEntity(PartitionKey, "a");
                var target = await GetTargetAsync();

                target.UpsertEntity(entity, TableUpdateMode.Replace);
                await target.SubmitBatchAsync();

                Assert.Single(await GetEntitiesAsync());
            }

            [Fact]
            public async Task MergesSingleEntity()
            {
                var entity = await AddEntityAsync("a", fieldA: "foo");
                var target = await GetTargetAsync();

                entity.FieldA = null;
                entity.FieldB = "bar";
                target.UpsertEntity(entity, TableUpdateMode.Replace);
                await target.SubmitBatchAsync();

                var output = Assert.Single(await GetEntitiesAsync());
                Assert.Null(output.FieldA);
                Assert.Equal("bar", output.FieldB);
            }

            [Fact]
            public async Task MergesMultipleEntities()
            {
                var entityA = new TestEntity(PartitionKey, "a")
                {
                    FieldA = "foo",
                    FieldB = "bar",
                };
                await AddEntityAsync("b");
                var entityC = await AddEntityAsync("c", fieldB: "baz");
                var target = await GetTargetAsync();

                entityC.FieldA = "qux";
                entityC.FieldB = null;
                target.UpsertEntity(entityA, TableUpdateMode.Replace);
                target.UpsertEntity(entityC, TableUpdateMode.Replace);
                await target.SubmitBatchAsync();

                var output = await GetEntitiesAsync();
                Assert.Equal(3, output.Count);
                Assert.Equal("foo", output[0].FieldA);
                Assert.Equal("bar", output[0].FieldB);
                Assert.Null(output[1].FieldA);
                Assert.Null(output[1].FieldB);
                Assert.Equal("qux", output[2].FieldA);
                Assert.Null(output[2].FieldB);
            }

            public TheUpsertReplaceEntityMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        private readonly Fixture _fixture;
        private readonly ITestOutputHelper _output;

        public MutableTableTransactionalBatchTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            PartitionKey = StorageUtility.GenerateDescendingId().ToString();
        }

        public string PartitionKey { get; }

        public async Task<IReadOnlyList<TestEntity>> GetEntitiesAsync()
        {
            var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
            return await table
                .QueryAsync<TestEntity>(x => x.PartitionKey == PartitionKey)
                .ToListAsync();
        }

        public async Task<TestEntity> AddEntityAsync(string rowKey, string fieldA = null, string fieldB = null)
        {
            var entity = new TestEntity(PartitionKey, rowKey)
            {
                FieldA = fieldA,
                FieldB = fieldB,
            };
            var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
            var response = await table.AddEntityAsync(entity);
            entity.UpdateETag(response);
            return entity;
        }

        public async Task<MutableTableTransactionalBatch> GetTargetAsync()
        {
            var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
            return new MutableTableTransactionalBatch(table);
        }

        public class Fixture : IAsyncLifetime
        {
            private bool _created;

            public Fixture()
            {
                Options = new Mock<IOptions<NuGetInsightsSettings>>();
                Settings = new NuGetInsightsSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                };
                Options.Setup(x => x.Value).Returns(() => Settings);
                TableName = TestSettings.NewStoragePrefix() + "1mttb1";
            }

            public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
            public NuGetInsightsSettings Settings { get; }
            public string TableName { get; }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public ServiceClientFactory GetServiceClientFactory(ILoggerFactory loggerFactory)
            {
                return new ServiceClientFactory(Options.Object, loggerFactory);
            }

            public async Task DisposeAsync()
            {
                await (await GetTableAsync(NullLoggerFactory.Instance)).DeleteAsync();
            }

            public async Task<TableClient> GetTableAsync(ILoggerFactory loggerFactory)
            {
                var table = (await GetServiceClientFactory(loggerFactory).GetTableServiceClientAsync())
                    .GetTableClient(TableName);

                if (!_created)
                {
                    await table.CreateIfNotExistsAsync();
                    _created = true;
                }

                return table;
            }
        }
    }
}
