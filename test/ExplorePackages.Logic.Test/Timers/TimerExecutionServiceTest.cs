using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    public class TimerExecutionServiceTest : IClassFixture<TimerExecutionServiceTest.Fixture>
    {
        public class TheConstructor : TimerExecutionServiceTest
        {
            public TheConstructor(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public void RejectsDuplicateNames()
            {
                var timer2 = new Mock<ITimer>();
                timer2.Setup(x => x.Name).Returns(TimerName);
                Timers.Add(timer2.Object);

                var ex = Assert.Throws<ArgumentException>(() => Target);

                Assert.Equal($"There are timers with duplicate names: '{TimerName}' (2)", ex.Message);
            }
        }

        public class TheSetIsEnabledMethod : TimerExecutionServiceTest
        {
            public TheSetIsEnabledMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SetsIsEnabledOnNewEntity(bool isEnabled)
            {
                await Target.SetIsEnabled(TimerName, isEnabled);

                var entities = await GetEntitiesAsync<DynamicTableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "IsEnabled" }, entity.Properties.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(isEnabled, entity.Properties["IsEnabled"].BooleanValue);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SetsIsEnabledOnExistingEntity(bool isEnabled)
            {
                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync(isEnabledDefault: true);
                var after = DateTimeOffset.UtcNow;

                await Target.SetIsEnabled(TimerName, isEnabled);

                var entities = await GetEntitiesAsync<DynamicTableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "IsEnabled", "LastExecuted" }, entity.Properties.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(isEnabled, entity.Properties["IsEnabled"].BooleanValue);
                Assert.InRange(entity.Properties["LastExecuted"].DateTimeOffsetValue.Value, before, after);
            }
        }

        public class TheExecuteNowAsyncMethod : TimerExecutionServiceTest
        {
            public TheExecuteNowAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Fact]
            public async Task IgnoresFrequency()
            {
                await Target.ExecuteNowAsync(TimerName);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteNowAsync(TimerName);
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }
        }

        public class TheExecuteAsyncMethod : TimerExecutionServiceTest
        {
            public TheExecuteAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SetsDefaultIsEnabled(bool isEnabledDefault)
            {
                await Target.ExecuteAsync(isEnabledDefault);

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(isEnabledDefault, entity.IsEnabled);
            }

            [Fact]
            public async Task CanExecuteNewTimerByDefault()
            {
                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync(isEnabledDefault: true);
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(TimerName, entity.RowKey);
                Assert.Empty(entity.PartitionKey);
                Assert.True(entity.IsEnabled);
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotExecuteTimerDisabledFromStorage()
            {
                await Target.SetIsEnabled(TimerName, isEnabled: false);

                await Target.ExecuteAsync(isEnabledDefault: true);

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
                Assert.Null(entity.LastExecuted);
            }

            [Fact]
            public async Task ExecutesTimerEnabledForStorage()
            {
                await Target.SetIsEnabled(TimerName, isEnabled: true);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync(isEnabledDefault: false);
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotRunATimerThatHasRunRecently()
            {
                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync(isEnabledDefault: true);
                var after = DateTimeOffset.UtcNow;

                await Target.ExecuteAsync(isEnabledDefault: true);

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task RunsATimerAgainIfTheFrequencyAllows()
            {
                await Target.ExecuteAsync(isEnabledDefault: true);

                Timer.Setup(x => x.Frequency).Returns(TimeSpan.Zero);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync(isEnabledDefault: true);
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotInsertNewTimerDisabledFromConfig()
            {
                Timer.Setup(x => x.IsEnabled).Returns(false);

                await Target.ExecuteAsync(isEnabledDefault: true);

                Assert.Empty(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotExecuteExistingTimerDisabledFromConfig()
            {
                await Target.ExecuteAsync(isEnabledDefault: true);
                var existingEntity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Invocations.Clear();
                Timer.Setup(x => x.IsEnabled).Returns(false);

                await Target.ExecuteAsync(isEnabledDefault: true);

                var newEntity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(existingEntity.ETag, newEntity.ETag);
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
            }
        }

        public TimerExecutionServiceTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
            TimerNamePrefix = StorageUtility.GenerateDescendingId().ToString();
            TimerName = TimerNamePrefix + "-a";
            Timer = new Mock<ITimer>();
            Timers = new List<ITimer> { Timer.Object };

            Timer.Setup(x => x.Name).Returns(TimerName);
            Timer.Setup(x => x.IsEnabled).Returns(true);
            Timer.Setup(x => x.Frequency).Returns(TimeSpan.FromMinutes(5));
        }

        private Fixture _fixture;
        private ITestOutputHelper _output;

        public string TimerNamePrefix { get; }
        public string TimerName { get; }
        public Mock<ITimer> Timer { get; }
        public List<ITimer> Timers { get; }
        public TimerExecutionService Target => new TimerExecutionService(
            _fixture.ServiceClientFactory,
            Timers,
            _fixture.LeaseService,
            _fixture.Options.Object,
            _output.GetTelemetryClient(),
            _output.GetLogger<TimerExecutionService>());
        public CloudTable Table => _fixture.GetTable();

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : ITableEntity, new()
        {
            return await Table.GetEntitiesAsync<T>(
                partitionKey: string.Empty,
                minRowKey: TimerNamePrefix,
                maxRowKey: TimerNamePrefix + char.MaxValue,
                _output.GetTelemetryClient().StartQueryLoopMetrics());
        }

        public class Fixture : IAsyncLifetime
        {
            public Fixture()
            {
                Options = new Mock<IOptions<ExplorePackagesSettings>>();
                Settings = new ExplorePackagesSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                    TimerTableName = "t" + StorageUtility.GenerateUniqueId().ToLowerInvariant(),
                    LeaseContainerName = "t" + StorageUtility.GenerateUniqueId().ToLowerInvariant(),
                };
                Options.Setup(x => x.Value).Returns(() => Settings);
                ServiceClientFactory = new ServiceClientFactory(Options.Object);
                LeaseService = new AutoRenewingStorageLeaseService(
                    new StorageLeaseService(
                        ServiceClientFactory,
                        Options.Object));
            }

            public Mock<IOptions<ExplorePackagesSettings>> Options { get; }
            public ExplorePackagesSettings Settings { get; }
            public ServiceClientFactory ServiceClientFactory { get; }
            public AutoRenewingStorageLeaseService LeaseService { get; }

            public async Task InitializeAsync()
            {
                await LeaseService.InitializeAsync();
                await GetTable().CreateIfNotExistsAsync();
            }

            public async Task DisposeAsync()
            {
                await ServiceClientFactory
                    .GetStorageAccount()
                    .CreateCloudBlobClient()
                    .GetContainerReference(Options.Object.Value.LeaseContainerName)
                    .DeleteIfExistsAsync();

                await GetTable().DeleteIfExistsAsync();
            }

            public CloudTable GetTable()
            {
                return ServiceClientFactory
                    .GetStorageAccount()
                    .CreateCloudTableClient()
                    .GetTableReference(Options.Object.Value.TimerTableName);
            }
        }
    }
}
