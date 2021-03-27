using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Microsoft.Extensions.Options;
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

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ETag", "IsEnabled", "PartitionKey", "RowKey", "Timestamp" }, entity.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(isEnabled, entity.GetBoolean("IsEnabled"));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SetsIsEnabledOnExistingEntity(bool isEnabled)
            {
                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync();
                var after = DateTimeOffset.UtcNow;

                await Target.SetIsEnabled(TimerName, isEnabled);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ETag", "IsEnabled", "LastExecuted", "PartitionKey", "RowKey", "Timestamp" }, entity.Keys.OrderBy(x => x).ToArray());
                Assert.Equal(isEnabled, entity.GetBoolean("IsEnabled"));
                Assert.InRange(entity.GetDateTimeOffset("LastExecuted").Value, before, after);
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

            [Fact]
            public async Task RunsEvenIfAlreadyRunning()
            {
                Timer.Setup(x => x.IsRunningAsync()).ReturnsAsync(true);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteNowAsync(TimerName);
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
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
            public async Task ObservesAutoStartProperty(bool autoStart)
            {
                Timer.Setup(x => x.AutoStart).Returns(autoStart);

                await Target.ExecuteAsync();

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(autoStart, entity.IsEnabled);
            }

            [Fact]
            public async Task CanExecuteNewTimerByDefault()
            {
                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync();
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(TimerName, entity.RowKey);
                Assert.Empty(entity.PartitionKey);
                Assert.True(entity.IsEnabled);
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task RunsIfAlreadyRunning()
            {
                Timer.Setup(x => x.IsRunningAsync()).ReturnsAsync(true);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync();
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotExecuteTimerDisabledFromStorage()
            {
                await Target.SetIsEnabled(TimerName, isEnabled: false);

                await Target.ExecuteAsync();

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
                Assert.Null(entity.LastExecuted);
            }

            [Fact]
            public async Task ExecutesTimerEnabledForStorage()
            {
                await Target.SetIsEnabled(TimerName, isEnabled: true);
                Timer.Setup(x => x.AutoStart).Returns(false);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync();
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotRunATimerThatHasRunRecently()
            {
                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync();
                var after = DateTimeOffset.UtcNow;

                await Target.ExecuteAsync();

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task RunsATimerAgainIfTheFrequencyAllows()
            {
                await Target.ExecuteAsync();

                Timer.Setup(x => x.Frequency).Returns(TimeSpan.Zero);

                var before = DateTimeOffset.UtcNow;
                await Target.ExecuteAsync();
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotInsertNewTimerDisabledFromConfig()
            {
                Timer.Setup(x => x.IsEnabled).Returns(false);

                await Target.ExecuteAsync();

                Assert.Empty(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotExecuteExistingTimerDisabledFromConfig()
            {
                await Target.ExecuteAsync();
                var existingEntity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Invocations.Clear();
                Timer.Setup(x => x.IsEnabled).Returns(false);

                await Target.ExecuteAsync();

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
            output.WriteLine("Using timer name prefix: " + TimerNamePrefix);
            TimerName = TimerNamePrefix + "-a";
            Timer = new Mock<ITimer>();
            Timers = new List<ITimer> { Timer.Object };

            Timer.Setup(x => x.Name).Returns(TimerName);
            Timer.Setup(x => x.IsEnabled).Returns(true);
            Timer.Setup(x => x.AutoStart).Returns(true);
            Timer.Setup(x => x.Frequency).Returns(TimeSpan.FromMinutes(5));
        }

        private readonly Fixture _fixture;
        private readonly ITestOutputHelper _output;

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
        public TableClient Table => _fixture.Table;

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            return await Table
                .QueryAsync<T>(x => x.PartitionKey == string.Empty
                                 && x.RowKey.CompareTo(TimerNamePrefix) >= 0
                                 && x.RowKey.CompareTo(TimerNamePrefix + char.MaxValue) < 0)
                .ToListAsync();
        }

        public class Fixture : IAsyncLifetime
        {
            public Fixture()
            {
                Options = new Mock<IOptions<ExplorePackagesSettings>>();
                Settings = new ExplorePackagesSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                    TimerTableName = TestSettings.NewStoragePrefix() + "1t1",
                    LeaseContainerName = TestSettings.NewStoragePrefix() + "1l1",
                };
                Options.Setup(x => x.Value).Returns(() => Settings);
                ServiceClientFactory = new ServiceClientFactory(Options.Object);
                LeaseService = new AutoRenewingStorageLeaseService(new StorageLeaseService(ServiceClientFactory, Options.Object));
            }

            public Mock<IOptions<ExplorePackagesSettings>> Options { get; }
            public ExplorePackagesSettings Settings { get; }
            public ServiceClientFactory ServiceClientFactory { get; }
            public AutoRenewingStorageLeaseService LeaseService { get; }
            public TableClient Table { get; private set; }

            public async Task InitializeAsync()
            {
                await LeaseService.InitializeAsync();
                Table = (await ServiceClientFactory.GetTableServiceClientAsync())
                    .GetTableClient(Options.Object.Value.TimerTableName);
                await Table.CreateIfNotExistsAsync(retry: true);
            }

            public async Task DisposeAsync()
            {
                await (await ServiceClientFactory.GetBlobServiceClientAsync())
                    .GetBlobContainerClient(Options.Object.Value.LeaseContainerName)
                    .DeleteIfExistsAsync();

                await Table.DeleteIfExistsAsync();
            }
        }
    }
}
