// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
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

namespace NuGet.Insights.WideEntities
{
    public class WideEntityServiceTest : IClassFixture<WideEntityServiceTest.Fixture>, IAsyncLifetime
    {
        public class ExecuteBatchAsync : WideEntityServiceTest
        {
            [Fact]
            public async Task SplitsBatches()
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var batch = new[]
                {
                    WideEntityOperation.Insert(partitionKey, "rk-1", Bytes.Slice(100, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-2", Bytes.Slice(200, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-3", Bytes.Slice(300, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-4", Bytes.Slice(400, WideEntityService.MaxTotalDataSize)),
                };

                // Act
                var inserted = await Target.ExecuteBatchAsync(TableName, batch, allowBatchSplits: true);

                // Assert
                var retrieved = await Target.RetrieveAsync(TableName, partitionKey);
                Assert.Equal(retrieved.Count, inserted.Count);
                for (var i = 0; i < inserted.Count; i++)
                {
                    Assert.Equal(retrieved[i].PartitionKey, inserted[i].PartitionKey);
                    Assert.Equal(retrieved[i].RowKey, inserted[i].RowKey);
                    Assert.Equal(retrieved[i].ETag, inserted[i].ETag);
                    Assert.Equal(retrieved[i].SegmentCount, inserted[i].SegmentCount);
                    Assert.Equal(retrieved[i].ToByteArray(), inserted[i].ToByteArray());
                }
            }

            [Fact]
            public async Task DeletesEntities()
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var inserted = await Target.ExecuteBatchAsync(TableName, new[]
                {
                    WideEntityOperation.Insert(partitionKey, "rk-1", Bytes.Slice(100, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-2", Bytes.Slice(200, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-3", Bytes.Slice(300, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-4", Bytes.Slice(400, WideEntityService.MaxTotalDataSize)),
                }, allowBatchSplits: true);
                var batch = inserted.Select(x => WideEntityOperation.Delete(x)).ToList();

                // Act
                await Target.ExecuteBatchAsync(TableName, batch, allowBatchSplits: true);

                // Assert
                var retrieved = await Target.RetrieveAsync(TableName, partitionKey);
                Assert.Empty(retrieved);
            }

            [Fact]
            public async Task FailsWhenCannotSplitBatch()
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var batch = new[]
                {
                    WideEntityOperation.Insert(partitionKey, "rk-1", Bytes.Slice(100, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-2", Bytes.Slice(200, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-3", Bytes.Slice(300, WideEntityService.MaxTotalDataSize)),
                    WideEntityOperation.Insert(partitionKey, "rk-4", Bytes.Slice(400, WideEntityService.MaxTotalDataSize)),
                };

                // Act
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => Target.ExecuteBatchAsync(TableName, batch, allowBatchSplits: false));
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (HttpStatusCode)ex.Status);
                Assert.Empty(await Target.RetrieveAsync(TableName, partitionKey));
            }

            public ExecuteBatchAsync(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class InsertOrReplaceAsync : WideEntityServiceTest
        {
            [Fact]
            public async Task InsertsWhenNoEntityExists()
            {
                // Arrange
                var content = Bytes.Slice(16 * 1024, 32 * 1024);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();

                // Act
                var newEntity = await Target.InsertOrReplaceAsync(TableName, partitionKey, rowKey, content);

                // Assert
                var retrieved = await Target.RetrieveAsync(TableName, partitionKey, rowKey);
                Assert.Equal(retrieved.ETag, newEntity.ETag);
                Assert.Equal(retrieved.ToByteArray(), newEntity.ToByteArray());
            }

            [Fact]
            public async Task ReplacesExistingEntity()
            {
                // Arrange
                var existingContent = Bytes.Slice(0, 1024 * 1024);
                var newContent = Bytes.Slice(16 * 1024, 32 * 1024);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();
                var existingEntity = await Target.InsertAsync(TableName, partitionKey, rowKey, existingContent);

                // Act
                var newEntity = await Target.InsertOrReplaceAsync(TableName, partitionKey, rowKey, newContent);

                // Assert
                Assert.NotEqual(existingEntity.ETag, newEntity.ETag);
                Assert.NotEqual(existingEntity.ToByteArray(), newEntity.ToByteArray());

                var retrieved = await Target.RetrieveAsync(TableName, partitionKey, rowKey);
                Assert.Equal(retrieved.ETag, newEntity.ETag);
                Assert.Equal(retrieved.ToByteArray(), newEntity.ToByteArray());
            }

            public InsertOrReplaceAsync(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class InsertAsync : WideEntityServiceTest
        {
            [Fact]
            public async Task FailsWhenEntityIsTooLarge()
            {
                // Arrange
                var content = Bytes.Slice(0, WideEntityService.MaxTotalDataSize + 1);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();

                // Act & Assert
                var ex = await Assert.ThrowsAsync<ArgumentException>(() => Target.InsertAsync(TableName, partitionKey, rowKey, content));
                Assert.Equal("The content is too large. (Parameter 'content')", ex.Message);
            }

            [Fact]
            public async Task FailsWhenEntityExists()
            {
                // Arrange
                var existingContent = Bytes.Slice(0, 1024 * 1024);
                var newContent = Bytes.Slice(16 * 1024, 32 * 1024);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();
                var existingEntity = await Target.InsertAsync(TableName, partitionKey, rowKey, existingContent);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => Target.InsertAsync(TableName, partitionKey, rowKey, newContent));
                Assert.Equal((int)HttpStatusCode.Conflict, ex.Status);
                var retrieved = await Target.RetrieveAsync(TableName, partitionKey, rowKey);
                Assert.Equal(existingEntity.ETag, retrieved.ETag);
                Assert.Equal(existingEntity.ToByteArray(), retrieved.ToByteArray());
            }

            public InsertAsync(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class ReplaceAsync : WideEntityServiceTest
        {
            [Fact]
            public async Task ReplacesExistingEntity()
            {
                // Arrange
                var existingContent = Bytes.Slice(0, 1024 * 1024);
                var newContent = Bytes.Slice(16 * 1024, 32 * 1024);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();
                var existingEntity = await Target.InsertAsync(TableName, partitionKey, rowKey, existingContent);

                // Act
                var newEntity = await Target.ReplaceAsync(TableName, existingEntity, newContent);

                // Assert
                Assert.NotEqual(existingEntity.ETag, newEntity.ETag);
                Assert.NotEqual(existingEntity.ToByteArray(), newEntity.ToByteArray());

                var retrieved = await Target.RetrieveAsync(TableName, partitionKey, rowKey);
                Assert.Equal(retrieved.ETag, newEntity.ETag);
                Assert.Equal(retrieved.ToByteArray(), newEntity.ToByteArray());
            }

            [Fact]
            public async Task FailsWhenExistingEntityHasBeenDeleted()
            {
                // Arrange
                var content = Bytes.Slice(0, 16);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();
                var existingEntity = await Target.InsertAsync(TableName, partitionKey, rowKey, content);
                await Target.DeleteAsync(TableName, existingEntity);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => Target.ReplaceAsync(TableName, existingEntity, content));
                Assert.Equal((int)HttpStatusCode.NotFound, ex.Status);
                Assert.Null(await Target.RetrieveAsync(TableName, partitionKey, rowKey));
            }

            [Fact]
            public async Task FailsWhenExistingEntityHasBeenChanged()
            {
                // Arrange
                var existingContent = Bytes.Slice(0, 1024 * 1024);
                var newContent = Bytes.Slice(16 * 1024, 32 * 1024);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();
                var existingEntity = await Target.InsertAsync(TableName, partitionKey, rowKey, existingContent);
                var changedEntity = await Target.ReplaceAsync(TableName, existingEntity, existingContent);

                // Act & Assert
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => Target.ReplaceAsync(TableName, existingEntity, newContent));
                Assert.Equal((int)HttpStatusCode.PreconditionFailed, ex.Status);
                var retrieved = await Target.RetrieveAsync(TableName, partitionKey, rowKey);
                Assert.Equal(changedEntity.ETag, retrieved.ETag);
                Assert.Equal(changedEntity.ToByteArray(), retrieved.ToByteArray());
            }

            public ReplaceAsync(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class RetrieveAsync_Range : WideEntityServiceTest
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsEmptyForNonExistentEntity(bool includeData)
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var minRowKey = "rk-1";
                var maxRowKey = "rk-3";

                // Act
                var actual = await Target.RetrieveAsync(TableName, partitionKey, minRowKey, maxRowKey, includeData);

                // Assert
                Assert.Empty(actual);
            }

            [Theory]
            [InlineData("a", "rk-1", 0, 1)]
            [InlineData("rk-0", "rk-1", 0, 1)]
            [InlineData("rk-1", "rk-1", 0, 1)]
            [InlineData("rk-1", "rk-2", 0, 2)]
            [InlineData("rk-2", "rk-3", 1, 2)]
            [InlineData("rk-2", "rk-4", 1, 2)]
            [InlineData("rk-2", "z", 1, 3)]
            [InlineData("rk-4", "rk-5", 3, 1)]
            [InlineData("rk-1", "rk-5", 0, 4)]
            [InlineData("a", "z", 0, 4)]
            public async Task FetchesRangeOfEntities(string minRowKey, string maxRowKey, int skip, int take)
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var a = await Target.InsertAsync(TableName, partitionKey, "rk-1", Bytes.Slice(0, 1024));
                var b = await Target.InsertAsync(TableName, partitionKey, "rk-2", Bytes.Slice(1024, 1024));
                var c = await Target.InsertAsync(TableName, partitionKey, "rk-3", Bytes.Slice(2048, 1024));
                var d = await Target.InsertAsync(TableName, partitionKey, "rk-5", Bytes.Slice(3072, 1024));
                var all = new[] { a, b, c, d };

                // Act
                var entities = await Target.RetrieveAsync(TableName, partitionKey, minRowKey, maxRowKey);

                // Assert
                Assert.Equal(take, entities.Count);
                for (var i = skip; i < skip + take; i++)
                {
                    var expected = all[i];
                    var actual = entities[i - skip];
                    Assert.Equal(expected.PartitionKey, actual.PartitionKey);
                    Assert.Equal(expected.RowKey, actual.RowKey);
                    Assert.Equal(expected.ETag, actual.ETag);
                    Assert.Equal(expected.ToByteArray(), actual.ToByteArray());
                }
            }

            [Fact]
            public async Task AllowsNotFetchingData()
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKeys = new[] { "rk-1", "rk-2", "rk-3" };
                var before = DateTimeOffset.UtcNow;
                await Target.InsertAsync(TableName, partitionKey, rowKeys[0], Bytes.Slice(0, 1024));
                await Target.InsertAsync(TableName, partitionKey, rowKeys[1], Bytes.Slice(1024, 1024));
                await Target.InsertAsync(TableName, partitionKey, rowKeys[2], Bytes.Slice(2048, 1024));
                var after = DateTimeOffset.UtcNow;

                // Act
                var actual = await Target.RetrieveAsync(TableName, partitionKey, rowKeys[0], rowKeys[2], includeData: false);

                // Assert
                Assert.Equal(3, actual.Count);
                Assert.All(actual, x => Assert.Equal(partitionKey, x.PartitionKey));
                Assert.Equal(rowKeys[0], actual[0].RowKey);
                Assert.Equal(rowKeys[1], actual[1].RowKey);
                Assert.Equal(rowKeys[2], actual[2].RowKey);
                var error = TimeSpan.FromMinutes(5);
                Assert.All(actual, x => Assert.NotEqual(default, x.ETag));
                Assert.All(actual, x => Assert.Equal(1, x.SegmentCount));
                Assert.All(actual, x =>
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => x.GetStream());
                    Assert.Equal("The data was not included when retrieving this entity.", ex.Message);
                });
            }

            public RetrieveAsync_Range(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class RetrieveAsync_PartitionKey : WideEntityServiceTest
        {
            public RetrieveAsync_PartitionKey(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsEmptyForNonExistentEntity(bool includeData)
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                await Target.InsertAsync(TableName, partitionKey, "rk-1", Bytes.Slice(0, 1024));

                // Act
                var actual = await Target.RetrieveAsync(TableName, partitionKey + "different", includeData);

                // Assert
                Assert.Empty(actual);
            }

            [Fact]
            public async Task ReturnsAllInPartitionKey()
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var a = await Target.InsertAsync(TableName, partitionKey, "rk-1", Bytes.Slice(0, 1024));
                var b = await Target.InsertAsync(TableName, partitionKey, "rk-2", Bytes.Slice(1024, 1024));
                var c = await Target.InsertAsync(TableName, partitionKey, "rk-3", Bytes.Slice(2048, 1024));
                var all = new[] { a, b, c };

                // Act
                var entities = await Target.RetrieveAsync(TableName, partitionKey);

                // Assert
                Assert.Equal(all.Length, entities.Count);
                for (var i = 0; i < all.Length; i++)
                {
                    var expected = all[i];
                    var actual = entities[i];
                    Assert.Equal(expected.PartitionKey, actual.PartitionKey);
                    Assert.Equal(expected.RowKey, actual.RowKey);
                    Assert.Equal(expected.ETag, actual.ETag);
                    Assert.Equal(expected.ToByteArray(), actual.ToByteArray());
                }
            }
        }


        public class RetrieveAsync_All : WideEntityServiceTest
        {
            public RetrieveAsync_All(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsEmptyForEmptyTable(bool includeData)
            {
                // Arrange & Act
                var actual = await Target.RetrieveAsync(TableName, includeData);

                // Assert
                Assert.Empty(actual);
            }

            [Fact]
            public async Task ReturnsAll()
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var a = await Target.InsertAsync(TableName, partitionKey + "-1", "rk-1", Bytes.Slice(0, 1024));
                var b = await Target.InsertAsync(TableName, partitionKey + "-2", "rk-2", Bytes.Slice(1024, 1024));
                var c = await Target.InsertAsync(TableName, partitionKey + "-3", "rk-3", Bytes.Slice(2048, 1024));
                var all = new[] { a, b, c };

                // Act
                var entities = await Target.RetrieveAsync(TableName);

                // Assert
                Assert.Equal(all.Length, entities.Count);
                for (var i = 0; i < all.Length; i++)
                {
                    var expected = all[i];
                    var actual = entities[i];
                    Assert.Equal(expected.PartitionKey, actual.PartitionKey);
                    Assert.Equal(expected.RowKey, actual.RowKey);
                    Assert.Equal(expected.ETag, actual.ETag);
                    Assert.Equal(expected.ToByteArray(), actual.ToByteArray());
                }
            }
        }

        public class RetrieveAsync : WideEntityServiceTest
        {
            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsNullForNonExistentEntity(bool includeData)
            {
                // Arrange
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();

                // Act
                var wideEntity = await Target.RetrieveAsync(TableName, partitionKey, rowKey, includeData);

                // Assert
                Assert.Null(wideEntity);
            }

            [Fact]
            public async Task AllowsNotFetchingData()
            {
                // Arrange
                var src = Bytes.Slice(0, 1024);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();
                var before = DateTimeOffset.UtcNow;
                await Target.InsertAsync(TableName, partitionKey, rowKey, src);
                var after = DateTimeOffset.UtcNow;

                // Act
                var wideEntity = await Target.RetrieveAsync(TableName, partitionKey, rowKey, includeData: false);

                // Assert
                Assert.Equal(partitionKey, wideEntity.PartitionKey);
                Assert.Equal(rowKey, wideEntity.RowKey);
                var error = TimeSpan.FromMinutes(5);
                Assert.NotEqual(default, wideEntity.ETag);
                Assert.Equal(1, wideEntity.SegmentCount);
                var ex = Assert.Throws<InvalidOperationException>(() => wideEntity.GetStream());
                Assert.Equal("The data was not included when retrieving this entity.", ex.Message);
            }

            public RetrieveAsync(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class IntegrationTest : WideEntityServiceTest
        {
            [Theory]
            [MemberData(nameof(RoundTripsBytesTestData))]
            public async Task RoundTripsBytes(int length)
            {
                // Arrange
                var src = Bytes.Slice(0, length);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();

                // Act
                await Target.InsertAsync(TableName, partitionKey, rowKey, src);
                var wideEntity = await Target.RetrieveAsync(TableName, partitionKey, rowKey);

                // Assert
                Assert.Equal(src.ToArray(), wideEntity.ToByteArray());
            }

            [Fact]
            public async Task PopulatesWideEntityProperties()
            {
                // Arrange
                var src = Bytes.Slice(0, WideEntityService.MaxTotalDataSize);
                var partitionKey = StorageUtility.GenerateDescendingId().ToString();
                var rowKey = StorageUtility.GenerateDescendingId().ToString();

                // Act
                var before = DateTimeOffset.UtcNow;
                await Target.InsertAsync(TableName, partitionKey, rowKey, src);
                var after = DateTimeOffset.UtcNow;
                var wideEntity = await Target.RetrieveAsync(TableName, partitionKey, rowKey);

                // Assert
                Assert.Equal(partitionKey, wideEntity.PartitionKey);
                Assert.Equal(rowKey, wideEntity.RowKey);
                var error = TimeSpan.FromMinutes(5);
                Assert.NotEqual(default, wideEntity.ETag);
                Assert.Equal(TestSettings.IsStorageEmulator ? 8 : 3, wideEntity.SegmentCount);
            }

            public static IEnumerable<object[]> RoundTripsBytesTestData => ByteArrayLengths
                .Distinct()
                .OrderBy(x => x)
                .Select(x => new object[] { x });

            private static IEnumerable<int> ByteArrayLengths
            {
                get
                {
                    yield return 0;
                    var current = 1;
                    do
                    {
                        yield return current;
                        current *= 2;
                    }
                    while (current <= WideEntityService.MaxTotalDataSize);

                    for (var i = 16; i >= 0; i--)
                    {
                        yield return WideEntityService.MaxTotalDataSize - i;
                    }

                    var random = new Random(0);
                    for (var i = 0; i < 10; i++)
                    {
                        yield return random.Next(0, WideEntityService.MaxTotalDataSize);
                    }
                }
            }

            public IntegrationTest(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        private readonly Fixture _fixture;
        private readonly ITestOutputHelper _output;

        public WideEntityServiceTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            Target = new WideEntityService(
                _fixture.GetServiceClientFactory(output.GetLogger<ServiceClientFactory>()),
                output.GetTelemetryClient(),
                _fixture.Options.Object);
        }

        public string TableName => _fixture.TableName;
        public ReadOnlyMemory<byte> Bytes => _fixture.Bytes.AsMemory();
        public WideEntityService Target { get; }

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
                Options = new Mock<IOptions<NuGetInsightsSettings>>();
                Settings = new NuGetInsightsSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                };
                Options.Setup(x => x.Value).Returns(() => Settings);
                TableName = TestSettings.NewStoragePrefix() + "1we1";

                Bytes = new byte[4 * 1024 * 1024];
                var random = new Random(0);
                random.NextBytes(Bytes);
            }

            public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
            public NuGetInsightsSettings Settings { get; }
            public string TableName { get; }
            public byte[] Bytes { get; }

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
