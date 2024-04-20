// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class CursorStorageServiceTest : IClassFixture<CursorStorageServiceTest.Fixture>, IAsyncLifetime
    {
        public class TheGetOrCreateAllAsyncMethod : CursorStorageServiceTest
        {
            public TheGetOrCreateAllAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsExistingCursors()
            {
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);
                var cursorA = await Target.GetOrCreateAsync(CursorName + "A");
                var cursorB = await Target.GetOrCreateAsync(CursorName + "B");

                var cursors = await Target.GetOrCreateAllAsync(new[] { cursorB.Name, cursorA.Name });

                Assert.Equal(2, cursors.Count);
                Assert.Equal(cursorB.Name, cursors[0].Name);
                Assert.Equal(cursorB.ETag, cursors[0].ETag);
                Assert.Equal(cursorA.Name, cursors[1].Name);
                Assert.Equal(cursorA.ETag, cursors[1].ETag);
            }

            [Fact]
            public async Task CreateNewCursors()
            {
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);

                var cursors = await Target.GetOrCreateAllAsync(new[] { CursorName + "B", CursorName + "A" });

                var entities = await GetEntitiesAsync<CursorTableEntity>();
                Assert.Equal(2, cursors.Count);
                Assert.Equal(entities[1].Name, cursors[0].Name);
                Assert.Equal(entities[1].ETag, cursors[0].ETag);
                Assert.Equal(entities[0].Name, cursors[1].Name);
                Assert.Equal(entities[0].ETag, cursors[1].ETag);
            }
        }

        public class TheGetOrCreateAsyncMethod : CursorStorageServiceTest
        {
            public TheGetOrCreateAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsExistingCursor()
            {
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);
                var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
                await table.AddEntityAsync(new CursorTableEntity(CursorName) { Value = value });

                var cursor = await Target.GetOrCreateAsync(CursorName);

                Assert.Equal(value, cursor.Value);
            }

            [Fact]
            public async Task CreatesNewCursor()
            {
                var cursor = await Target.GetOrCreateAsync(CursorName);

                Assert.Equal(CursorTableEntity.Min, cursor.Value);
                var entities = await GetEntitiesAsync<CursorTableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(cursor.Value, entity.Value);
                Assert.Equal(cursor.PartitionKey, entity.PartitionKey);
                Assert.Equal(cursor.RowKey, entity.RowKey);
                Assert.Equal(CursorName, entity.Name);
                Assert.Equal(cursor.ETag, entity.ETag);
            }

            [Fact]
            public async Task HasExpectedProperties()
            {
                var cursor = await Target.GetOrCreateAsync(CursorName);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "PartitionKey", "RowKey", "Timestamp", "Value", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            }
        }

        public class TheUpdateAsyncMethod : CursorStorageServiceTest
        {
            public TheUpdateAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task UpdatesValue()
            {
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);
                var cursor = await Target.GetOrCreateAsync(CursorName);
                cursor.Value = value;

                await Target.UpdateAsync(cursor);

                cursor = await Target.GetOrCreateAsync(CursorName);
                Assert.Equal(value, cursor.Value);
            }

            [Fact]
            public async Task AllowsSubsequentUpdates()
            {
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);
                var cursor = await Target.GetOrCreateAsync(CursorName);
                cursor.Value = value;

                await Target.UpdateAsync(cursor);
                cursor.Value = cursor.Value.AddMinutes(1);
                await Target.UpdateAsync(cursor);
                cursor.Value = cursor.Value.AddMinutes(1);
                await Target.UpdateAsync(cursor);

                cursor = await Target.GetOrCreateAsync(CursorName);
                Assert.Equal(value.AddMinutes(2), cursor.Value);
            }

            [Fact]
            public async Task FailsIfAnotherActorUpdatedEntity()
            {
                var cursor = await Target.GetOrCreateAsync(CursorName);
                await Target.UpdateAsync(await Target.GetOrCreateAsync(CursorName));
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);

                await Assert.ThrowsAsync<RequestFailedException>(() => Target.UpdateAsync(cursor));

                cursor = await Target.GetOrCreateAsync(CursorName);
                Assert.Equal(CursorTableEntity.Min, cursor.Value);
            }
        }

        public CursorStorageServiceTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            CursorNamePrefix = StorageUtility.GenerateDescendingId().ToString();
            CursorName = CursorNamePrefix + "-a";
        }

        private readonly Fixture _fixture;
        private readonly ITestOutputHelper _output;

        public string CursorNamePrefix { get; }
        public string CursorName { get; }
        public CursorStorageService Target => new CursorStorageService(
            _fixture.GetServiceClientFactory(_output.GetLoggerFactory()),
            _fixture.Options.Object,
            _output.GetLogger<CursorStorageService>());

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
            return await table
                .QueryAsync<T>(x => x.PartitionKey == string.Empty
                                 && x.RowKey.CompareTo(CursorNamePrefix) >= 0
                                 && x.RowKey.CompareTo(CursorNamePrefix + char.MaxValue) < 0)
                .ToListAsync();
        }

        public async Task InitializeAsync()
        {
            await _fixture.GetTableAsync(_output.GetLoggerFactory());
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        public class Fixture : IAsyncLifetime
        {
            public bool _created;

            public Fixture()
            {
                Options = new Mock<IOptions<NuGetInsightsWorkerSettings>>();
                Settings = new NuGetInsightsWorkerSettings
                {
                    CursorTableName = LogicTestSettings.NewStoragePrefix() + "1c1",
                }.WithTestStorageSettings();
                Options.Setup(x => x.Value).Returns(() => Settings);
            }

            public Mock<IOptions<NuGetInsightsWorkerSettings>> Options { get; }
            public NuGetInsightsWorkerSettings Settings { get; }

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

            public async Task<TableClientWithRetryContext> GetTableAsync(ILoggerFactory loggerFactory)
            {
                var table = (await GetServiceClientFactory(loggerFactory).GetTableServiceClientAsync())
                    .GetTableClient(Options.Object.Value.CursorTableName);

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
