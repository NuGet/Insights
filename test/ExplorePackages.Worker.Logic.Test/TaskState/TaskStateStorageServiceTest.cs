using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class TaskStateStorageServiceTest : IClassFixture<TaskStateStorageServiceTest.Fixture>
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

        public class TheAddAsyncMethod : TaskStateStorageServiceTest
        {
            public TheAddAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task CanAddMoreThan100TaskStatesAtOnce()
            {
                var rowKeys = Enumerable.Range(100, 101).Select(x => x.ToString()).ToList();

                await Target.AddAsync(StorageSuffix, PartitionKey, rowKeys);

                var taskStates = await GetEntitiesAsync<TaskState>();
                Assert.Equal(rowKeys, taskStates.Select(x => x.RowKey).ToList());
                Assert.All(taskStates, x =>
                {
                    Assert.Equal(StorageSuffix, x.StorageSuffix);
                    Assert.Equal(PartitionKey, x.PartitionKey);
                    Assert.Null(x.Parameters);
                    Assert.NotNull(x.Timestamp);
                    Assert.NotEqual(default, x.ETag);
                });
            }

            [Fact]
            public async Task DoesNotConflictWithExistin()
            {
                var rowKeysA = Enumerable.Range(10, 10).Select(x => x.ToString()).ToList();
                var rowKeysB = Enumerable.Range(10, 20).Select(x => x.ToString()).ToList();

                await Target.AddAsync(StorageSuffix, PartitionKey, rowKeysA);
                await Target.AddAsync(StorageSuffix, PartitionKey, rowKeysB);

                var taskStates = await GetEntitiesAsync<TaskState>();
                Assert.Equal(rowKeysB, taskStates.Select(x => x.RowKey).ToList());
                Assert.All(taskStates, x =>
                {
                    Assert.Equal(StorageSuffix, x.StorageSuffix);
                    Assert.Equal(PartitionKey, x.PartitionKey);
                    Assert.Null(x.Parameters);
                    Assert.NotNull(x.Timestamp);
                    Assert.NotEqual(default, x.ETag);
                });
            }

            [Fact]
            public async Task HasExpectedPropertiesWithoutParameters()
            {
                await Target.AddAsync(new TaskStateKey(StorageSuffix, PartitionKey, "foo"));

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ETag", "PartitionKey", "RowKey", "StorageSuffix", "Timestamp" }, entity.Keys.OrderBy(x => x).ToArray());
            }

            [Fact]
            public async Task HasExpectedPropertiesWithParameters()
            {
                await Target.AddAsync(StorageSuffix, PartitionKey, new[] { new TaskState(StorageSuffix, PartitionKey, "bar") { Parameters = "baz" } });

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ETag", "Parameters", "PartitionKey", "RowKey", "StorageSuffix", "Timestamp" }, entity.Keys.OrderBy(x => x).ToArray());
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
            _fixture.ServiceClientFactory,
            _output.GetTelemetryClient(),
            _fixture.Options.Object);
        public TableClient Table => _fixture.Table;

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            return await Table
                .QueryAsync<T>(x => x.PartitionKey == PartitionKey)
                .ToListAsync();
        }

        public class Fixture : IAsyncLifetime
        {
            public Fixture()
            {
                Options = new Mock<IOptions<ExplorePackagesWorkerSettings>>();
                Settings = new ExplorePackagesWorkerSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                    TaskStateTableName = TestSettings.NewStoragePrefix() + "1ts1",
                };
                Options.Setup(x => x.Value).Returns(() => Settings);
                ServiceClientFactory = new ServiceClientFactory(Options.Object);
                StorageSuffix = "suffix";
            }

            public Mock<IOptions<ExplorePackagesWorkerSettings>> Options { get; }
            public ExplorePackagesWorkerSettings Settings { get; }
            public ServiceClientFactory ServiceClientFactory { get; }
            public string StorageSuffix { get; }
            public TableClient Table { get; private set; }

            public async Task InitializeAsync()
            {
                Table = await GetTableAsync();
                await Table.CreateIfNotExistsAsync();
            }

            public async Task DisposeAsync()
            {
                await (await GetTableAsync()).DeleteIfExistsAsync();
            }

            public async Task<TableClient> GetTableAsync()
            {
                return (await ServiceClientFactory.GetTableServiceClientAsync())
                    .GetTableClient(Options.Object.Value.TaskStateTableName + StorageSuffix);
            }
        }
    }
}
