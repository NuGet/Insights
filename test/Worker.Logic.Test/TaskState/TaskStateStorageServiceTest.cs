// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;
using Azure.Data.Tables;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.Worker
{
    public class TaskStateStorageServiceTest : IClassFixture<TaskStateStorageServiceTest.Fixture>, IAsyncLifetime
    {
        public class TheGetAsyncMethod : TaskStateStorageServiceTest
        {
            public TheGetAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsNullForMissingTaskState()
            {
                var result = await Target.GetAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo"));

                Assert.Null(result);
            }
        }

        public class TheSetStartedAsyncMethod : TaskStateStorageServiceTest
        {
            public TheSetStartedAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsFalseIfDoesNotExist()
            {
                var exists = await Target.SetStartedAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo"));

                Assert.False(exists);
            }

            [Fact]
            public async Task LeavesExistingPropertiesWhenUpdating()
            {
                var taskStateBefore = new TaskState(StorageSuffix, PartitionKey, "foo") { Message = "bar", Parameters = "baz" };
                await Target.GetOrAddAsync(taskStateBefore);

                var exists = await Target.SetStartedAsync(taskStateBefore.GetKey());

                Assert.True(exists);
                Assert.Null(taskStateBefore.Started);
                Assert.NotEqual(default, taskStateBefore.ETag);
                var taskStateAfter = await Target.GetAsync(taskStateBefore.GetKey());
                Assert.NotNull(taskStateAfter);
                Assert.NotEqual(taskStateBefore.ETag, taskStateAfter.ETag);
                Assert.NotNull(taskStateAfter.Started);
                Assert.Equal("bar", taskStateAfter.Message);
                Assert.Equal("baz", taskStateAfter.Parameters);
            }
        }

        public class TheSetMessageAsyncMethod : TaskStateStorageServiceTest
        {
            public TheSetMessageAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task FailsIfDoesNotExist()
            {
                var ex = await Assert.ThrowsAsync<RequestFailedException>(
                    () => Target.SetMessageAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo"), "bar"));
                Assert.Equal((int)HttpStatusCode.NotFound, ex.Status);
            }

            [Fact]
            public async Task LeavesExistingPropertiesWhenUpdating()
            {
                var taskStateBefore = new TaskState(StorageSuffix, PartitionKey, "foo")
                {
                    Message = "foo",
                    Parameters = "bar",
                    Started = new DateTimeOffset(2024, 10, 1, 9, 5, 0, TimeSpan.Zero),
                };
                await Target.GetOrAddAsync(taskStateBefore);

                await Target.SetMessageAsync(taskStateBefore.GetKey(), "qux");

                Assert.NotEqual(default, taskStateBefore.ETag);
                var taskStateAfter = await Target.GetAsync(taskStateBefore.GetKey());
                Assert.NotNull(taskStateAfter);
                Assert.NotEqual(taskStateBefore.ETag, taskStateAfter.ETag);
                Assert.Equal("qux", taskStateAfter.Message);
                Assert.Equal("bar", taskStateAfter.Parameters);
                Assert.Equal(new DateTimeOffset(2024, 10, 1, 9, 5, 0, TimeSpan.Zero), taskStateAfter.Started);
            }
        }

        public class TheGetOrAddAsyncMethod : TaskStateStorageServiceTest
        {
            public TheGetOrAddAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsNewlyAddedAndExisting()
            {
                var rowKeysA = Enumerable.Range(10, 10).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
                var rowKeysB = Enumerable.Range(10, 20).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeysA);

                var actual = await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeysB);

                Assert.Equal(20, actual.Count);
                Assert.Equal(rowKeysB.ToList(), actual.Select(x => x.RowKey).ToList());
                Assert.All(actual, x => Assert.NotEqual(default, x.ETag));
            }

            [Fact]
            public async Task ReturnsOnlyInput()
            {
                var rowKeysA = Enumerable.Range(10, 10).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
                var rowKeysB = Enumerable.Range(10, 10).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeysA);

                var actual = await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeysB);

                Assert.Equal(10, actual.Count);
                Assert.Equal(rowKeysB.ToList(), actual.Select(x => x.RowKey).ToList());
            }

            [Fact]
            public async Task CanAddMoreThan100TaskStatesAtOnce()
            {
                var rowKeys = Enumerable.Range(100, 101).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();

                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeys);

                var taskStates = await GetEntitiesAsync<TaskState>();
                Assert.Equal(rowKeys, taskStates.Select(x => x.RowKey).ToList());
                Assert.All(taskStates, x =>
                {
                    Assert.Equal(StorageSuffix, x.StorageSuffix);
                    Assert.Equal(PartitionKey, x.PartitionKey);
                    Assert.Null(x.Parameters);
                    Assert.NotEqual(default, x.ETag);
                });
            }

            [Fact]
            public async Task DoesNotConflictWithExisting()
            {
                var rowKeysA = Enumerable.Range(10, 10).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();
                var rowKeysB = Enumerable.Range(10, 20).Select(x => x.ToString(CultureInfo.InvariantCulture)).ToList();

                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeysA);
                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeysB);

                var taskStates = await GetEntitiesAsync<TaskState>();
                Assert.Equal(rowKeysB, taskStates.Select(x => x.RowKey).ToList());
                Assert.All(taskStates, x =>
                {
                    Assert.Equal(StorageSuffix, x.StorageSuffix);
                    Assert.Equal(PartitionKey, x.PartitionKey);
                    Assert.Null(x.Parameters);
                    Assert.NotEqual(default, x.ETag);
                });
            }

            [Fact]
            public async Task HasExpectedPropertiesWithoutParameters()
            {
                await Target.GetOrAddAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo"));

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "PartitionKey", "RowKey", "StorageSuffix", "Timestamp", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            }

            [Fact]
            public async Task HasExpectedPropertiesWithParameters()
            {
                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, new[] { new TaskState(StorageSuffix, PartitionKey, "bar") { Parameters = "baz" } });

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "Parameters", "PartitionKey", "RowKey", "StorageSuffix", "Timestamp", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
            }

            [Fact]
            public async Task CanAddTaskStateInstance()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo");

                await Target.GetOrAddAsync(input);

                var output = await Target.GetAsync(input.GetKey());
                Assert.Equal(output.ETag, input.ETag);
            }
        }

        public class TheDeleteAsyncMethod : TaskStateStorageServiceTest
        {
            public TheDeleteAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsTrueIfTaskStateExists()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo");
                await Target.GetOrAddAsync(input);

                var output = await Target.DeleteAsync(input);

                Assert.True(output);
                Assert.Null(await Target.GetAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo")));
            }

            [Fact]
            public async Task ReturnsTrueIETagHasChangedAndStateExists()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo");
                await Target.GetOrAddAsync(input);
                await Target.UpdateAsync(new TaskState(StorageSuffix, PartitionKey, "foo") { Parameters = "a", ETag = input.ETag });

                var output = await Target.DeleteAsync(input);

                Assert.True(output);
                Assert.Null(await Target.GetAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo")));
            }

            [Fact]
            public async Task ReturnsFalseIfTaskStateNoLongerExists()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo");
                await Target.GetOrAddAsync(input);
                await Target.DeleteAsync(input);

                var output = await Target.DeleteAsync(input);

                Assert.False(output);
                Assert.Null(await Target.GetAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo")));
            }

            [Fact]
            public async Task ReturnsFalseIfTaskStateNeverExisted()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo");

                var output = await Target.DeleteAsync(input);

                Assert.False(output);
                Assert.Null(await Target.GetAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo")));
            }
        }

        public class TheUpdateAsyncMethod : TaskStateStorageServiceTest
        {
            public TheUpdateAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task UpdatesContent()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo") { Parameters = "a" };
                await Target.GetOrAddAsync(input);
                input.Parameters = "b";

                await Target.UpdateAsync(input);

                var output = await Target.GetAsync(input.GetKey());
                Assert.Equal(output.ETag, input.ETag);
                Assert.Equal("b", output.Parameters);
            }

            [Fact]
            public async Task CanClearParameters()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo") { Parameters = "a" };
                await Target.GetOrAddAsync(input);
                input.Parameters = null;

                await Target.UpdateAsync(input);

                var output = await Target.GetAsync(input.GetKey());
                Assert.Null(output.Parameters);
            }

            [Fact]
            public async Task FailsIfETagChanged()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo") { Parameters = "a" };
                await Target.GetOrAddAsync(input);
                await Target.UpdateAsync(new TaskState(StorageSuffix, PartitionKey, "foo") { Parameters = "a", ETag = input.ETag });
                input.Parameters = "b";

                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => Target.UpdateAsync(input));
                Assert.Equal(HttpStatusCode.PreconditionFailed, (HttpStatusCode)ex.Status);
            }

            [Fact]
            public async Task FailsIfDeleted()
            {
                var input = new TaskState(StorageSuffix, PartitionKey, "foo") { Parameters = "a" };
                await Target.GetOrAddAsync(input);
                await Target.DeleteAsync(input);

                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => Target.UpdateAsync(input));
                Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)ex.Status);
            }
        }

        public class TheGetByRowKeyPrefixAsyncMethod : TaskStateStorageServiceTest
        {
            public TheGetByRowKeyPrefixAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task ReturnsNothingWhenNoMatch()
            {
                await Target.GetOrAddAsync(new TaskState(StorageSuffix, PartitionKey, "foo"));

                var actual = await Target.GetByRowKeyPrefixAsync(StorageSuffix, PartitionKey, "bar");

                Assert.Empty(actual);
            }

            [Fact]
            public async Task AllowsEmptyPrefix()
            {
                await Target.GetOrAddAsync(new TaskState(StorageSuffix, PartitionKey, "foo"));

                var actual = await Target.GetByRowKeyPrefixAsync(StorageSuffix, PartitionKey, "");

                Assert.Equal("foo", Assert.Single(actual).RowKey);
            }

            [Fact]
            public async Task ReturnsAllMatches()
            {
                var rowKeys = Enumerable.Range(0, 10).Select(x => $"foo{x}").ToList();
                await Target.GetOrAddAsync(StorageSuffix, PartitionKey, rowKeys);

                var actual = await Target.GetByRowKeyPrefixAsync(StorageSuffix, PartitionKey, "foo");

                Assert.Equal(rowKeys, actual.Select(x => x.RowKey).ToList());
                Assert.All(actual, x => Assert.Equal(x.PartitionKey, PartitionKey));
                Assert.All(actual, x => Assert.Equal(x.StorageSuffix, StorageSuffix));
            }
        }

        public TaskStateStorageServiceTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            PartitionKey = StorageUtility.GenerateDescendingId().ToString();
        }

        private readonly Fixture _fixture;
        private readonly ITestOutputHelper _output;

        public string StorageSuffix => _fixture.StorageSuffix;
        public string PartitionKey { get; }
        public TaskStateStorageService Target => new TaskStateStorageService(
            _fixture.GetServiceClientFactory(_output.GetLoggerFactory()),
            new EntityUpsertStorageService<TaskState, TaskState>(
                _output.GetTelemetryClient(),
                _output.GetLogger<EntityUpsertStorageService<TaskState, TaskState>>()),
            _output.GetTelemetryClient(),
            _fixture.Options.Object);

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
            return await table
                .QueryAsync<T>(x => x.PartitionKey == PartitionKey)
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
                    TaskStateTableNamePrefix = LogicTestSettings.NewStoragePrefix() + "1ts1",
                }.WithTestStorageSettings();
                Options.Setup(x => x.Value).Returns(() => Settings);
                StorageSuffix = "suffix";
            }

            public Mock<IOptions<NuGetInsightsWorkerSettings>> Options { get; }
            public NuGetInsightsWorkerSettings Settings { get; }
            public string StorageSuffix { get; }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public ServiceClientFactory GetServiceClientFactory(ILoggerFactory loggerFactory)
            {
                return new ServiceClientFactory(Options.Object, loggerFactory.GetLoggerTelemetryClient(), loggerFactory);
            }

            public async Task DisposeAsync()
            {
                await (await GetTableAsync(NullLoggerFactory.Instance)).DeleteAsync();
            }

            public async Task<TableClientWithRetryContext> GetTableAsync(ILoggerFactory loggerFactory)
            {
                var table = (await GetServiceClientFactory(loggerFactory).GetTableServiceClientAsync())
                    .GetTableClient(Options.Object.Value.TaskStateTableNamePrefix + StorageSuffix);

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
