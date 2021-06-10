// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public class CursorStorageServiceTest : IClassFixture<CursorStorageServiceTest.Fixture>, IAsyncLifetime
    {
        public class TheGetOrCreateAsyncMethod : CursorStorageServiceTest
        {
            public TheGetOrCreateAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsExistingCursor()
            {
                var value = new DateTimeOffset(2020, 1, 1, 12, 30, 0, TimeSpan.Zero);
                var table = await _fixture.GetTableAsync(_output.GetLogger<ServiceClientFactory>());
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
                Assert.Equal(CursorName, entity.GetName());
                Assert.Equal(cursor.ETag, entity.ETag);
            }

            [Fact]
            public async Task HasExpectedProperties()
            {
                var cursor = await Target.GetOrCreateAsync(CursorName);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "odata.etag", "PartitionKey", "RowKey", "Timestamp", "Value" }, entity.Keys.OrderBy(x => x).ToArray());
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
            _fixture.GetServiceClientFactory(_output.GetLogger<ServiceClientFactory>()),
            _fixture.Options.Object,
            _output.GetLogger<CursorStorageService>());

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            var table = await _fixture.GetTableAsync(_output.GetLogger<ServiceClientFactory>());
            return await table
                .QueryAsync<T>(x => x.PartitionKey == string.Empty
                                 && x.RowKey.CompareTo(CursorNamePrefix) >= 0
                                 && x.RowKey.CompareTo(CursorNamePrefix + char.MaxValue) < 0)
                .ToListAsync();
        }

        public async Task InitializeAsync()
        {
            await _fixture.GetTableAsync(_output.GetLogger<ServiceClientFactory>());
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
                    StorageConnectionString = TestSettings.StorageConnectionString,
                    CursorTableName = TestSettings.NewStoragePrefix() + "1c1",
                };
                Options.Setup(x => x.Value).Returns(() => Settings);
            }

            public Mock<IOptions<NuGetInsightsWorkerSettings>> Options { get; }
            public NuGetInsightsWorkerSettings Settings { get; }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public ServiceClientFactory GetServiceClientFactory(ILogger<ServiceClientFactory> logger)
            {
                return new ServiceClientFactory(Options.Object, logger);
            }

            public async Task DisposeAsync()
            {
                await (await GetTableAsync(NullLogger<ServiceClientFactory>.Instance)).DeleteAsync();
            }

            public async Task<TableClient> GetTableAsync(ILogger<ServiceClientFactory> logger)
            {
                var table = (await GetServiceClientFactory(logger).GetTableServiceClientAsync())
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
