// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Data.Tables;
using NCrontab;
using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights
{
    public class TimerExecutionServiceTest : IClassFixture<TimerExecutionServiceTest.Fixture>, IAsyncLifetime
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
                await Target.SetIsEnabledAsync(TimerName, isEnabled);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "IsEnabled", "PartitionKey", "RowKey", "Timestamp", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
                Assert.Equal(isEnabled, entity.GetBoolean("IsEnabled"));
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SetsIsEnabledOnExistingEntity(bool isEnabled)
            {
                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                await Target.SetIsEnabledAsync(TimerName, isEnabled);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "IsEnabled", "LastExecuted", "PartitionKey", "RowKey", "Timestamp", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
                Assert.Equal(isEnabled, entity.GetBoolean("IsEnabled"));
                Assert.InRange(entity.GetDateTimeOffset("LastExecuted").Value, before, after);
            }
        }

        public class TheSetNextRunMethod : TimerExecutionServiceTest
        {
            public TheSetNextRunMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task SetsNextRunOnNewEntity(bool autoStart)
            {
                Timer.Setup(x => x.AutoStart).Returns(autoStart);
                var nextRun = DateTimeOffset.UtcNow + TimeSpan.FromDays(1);

                await Target.SetNextRunAsync(TimerName, nextRun);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "IsEnabled", "NextRun", "PartitionKey", "RowKey", "Timestamp", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
                Assert.Equal(autoStart, entity.GetBoolean("IsEnabled"));
                Assert.Equal(nextRun, entity.GetDateTimeOffset("NextRun"));
            }

            [Fact]
            public async Task SetsNextRunOnExistingEntity()
            {
                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;
                var nextRun = DateTimeOffset.UtcNow + TimeSpan.FromDays(1);

                await Target.SetNextRunAsync(TimerName, nextRun);

                var entities = await GetEntitiesAsync<TableEntity>();
                var entity = Assert.Single(entities);
                Assert.Equal(new[] { "ClientRequestId", "IsEnabled", "LastExecuted", "NextRun", "PartitionKey", "RowKey", "Timestamp", "odata.etag" }, entity.Keys.OrderBy(x => x, StringComparer.Ordinal).ToArray());
                Assert.True(entity.GetBoolean("IsEnabled"));
                Assert.InRange(entity.GetDateTimeOffset("LastExecuted").Value, before, after);
                Assert.Equal(nextRun, entity.GetDateTimeOffset("NextRun"));
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
                Assert.True(await Target.ExecuteNowAsync(TimerName));

                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteNowAsync(TimerName));
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
                Assert.True(await Target.ExecuteNowAsync(TimerName));
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task ReturnsFalseIfTimerFails()
            {
                Timer.Setup(x => x.ExecuteAsync()).ThrowsAsync(new InvalidOperationException());

                var before = DateTimeOffset.UtcNow;
                var executed = await Target.ExecuteNowAsync(TimerName);
                var after = DateTimeOffset.UtcNow;

                Assert.False(executed);
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

                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(autoStart, entity.IsEnabled);
            }

            [Fact]
            public async Task CanExecuteNewTimerByDefault()
            {
                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Assert.Equal(TimerName, entity.RowKey);
                Assert.Equal(TimerExecutionService.PartitionKey, entity.PartitionKey);
                Assert.True(entity.IsEnabled);
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task RunsIfAlreadyRunning()
            {
                Timer.Setup(x => x.IsRunningAsync()).ReturnsAsync(true);

                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotExecuteTimerDisabledFromStorage()
            {
                await Target.SetIsEnabledAsync(TimerName, isEnabled: false);

                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
                Assert.Null(entity.LastExecuted);
            }

            [Fact]
            public async Task ExecutesTimerEnabledForStorage()
            {
                await Target.SetIsEnabledAsync(TimerName, isEnabled: true);
                Timer.Setup(x => x.AutoStart).Returns(false);

                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task UpdatesLastExecutedWhenExecuteThrows()
            {
                Timer.Setup(x => x.ExecuteAsync()).ThrowsAsync(new InvalidOperationException());

                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotAddNewTimerWhenExecuteReturnsFalse()
            {
                Timer.Setup(x => x.ExecuteAsync()).ReturnsAsync(false);

                Assert.True(await Target.ExecuteAsync());

                Assert.Empty(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
            }

            [Fact]
            public async Task DoesNotUpdateLastExecutedWhenExecuteReturnsFalse()
            {
                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;
                Timer.Setup(x => x.ExecuteAsync()).ReturnsAsync(false);
                Timer.Setup(x => x.Frequency).Returns(new TimerFrequency(TimeSpan.Zero));

                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task DoesNotRunATimerThatHasRunRecently()
            {
                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Once);
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task RunsATimerAgainIfTheTimeSpanFrequencyAllows()
            {
                Assert.True(await Target.ExecuteAsync());

                Timer.Setup(x => x.Frequency).Returns(new TimerFrequency(TimeSpan.Zero));

                var before = DateTimeOffset.UtcNow;
                Assert.True(await Target.ExecuteAsync());
                var after = DateTimeOffset.UtcNow;

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
                Assert.InRange(entity.LastExecuted.Value, before, after);
            }

            [Fact]
            public async Task RunsATimerAgainIfTheTimeSpanFrequencyAllowsMultipleTimeWindows()
            {
                Now = new DateTimeOffset(2025, 3, 22, 12, 55, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());

                Timer.Setup(x => x.Frequency).Returns(new TimerFrequency(TimeSpan.FromMinutes(30)));

                Now = new DateTimeOffset(2025, 3, 22, 13, 29, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
            }

            [Fact]
            public async Task RunsATimerAgainIfTheScheduleFrequencyAllows()
            {
                Now = new DateTimeOffset(2025, 3, 22, 12, 55, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());

                var schedule = CrontabSchedule.Parse("0,30 * * * *");
                Timer.Setup(x => x.Frequency).Returns(new TimerFrequency(schedule));

                Now = new DateTimeOffset(2025, 3, 22, 13, 5, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());
                Now = new DateTimeOffset(2025, 3, 22, 13, 29, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(2));
            }

            [Fact]
            public async Task RunsATimerAgainIfTheScheduleFrequencyAllowsMultipleTimeWindows()
            {
                Now = new DateTimeOffset(2025, 3, 22, 12, 55, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());

                var schedule = CrontabSchedule.Parse("0,30 * * * *");
                Timer.Setup(x => x.Frequency).Returns(new TimerFrequency(schedule));

                Now = new DateTimeOffset(2025, 3, 22, 13, 5, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());
                Now = new DateTimeOffset(2025, 3, 22, 13, 30, 0, TimeSpan.Zero);
                Assert.True(await Target.ExecuteAsync());

                var entity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Exactly(3));
            }

            [Fact]
            public async Task DoesNotInsertNewTimerDisabledFromConfig()
            {
                Timer.Setup(x => x.IsEnabled).Returns(false);

                Assert.True(await Target.ExecuteAsync());

                Assert.Empty(await GetEntitiesAsync<TimerEntity>());
                Timer.Verify(x => x.ExecuteAsync(), Times.Never);
            }

            [Fact]
            public async Task DoesNotExecuteExistingTimerDisabledFromConfig()
            {
                Assert.True(await Target.ExecuteAsync());
                var existingEntity = Assert.Single(await GetEntitiesAsync<TimerEntity>());
                Timer.Invocations.Clear();
                Timer.Setup(x => x.IsEnabled).Returns(false);

                Assert.True(await Target.ExecuteAsync());

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
            Timer.Setup(x => x.Frequency).Returns(new TimerFrequency(TimeSpan.FromMinutes(5)));
            Timer.Setup(x => x.ExecuteAsync()).ReturnsAsync(true);

            TimerComparer = new Mock<IComparer<ITimer>>();
        }

        private readonly Fixture _fixture;
        private readonly ITestOutputHelper _output;

        public string TimerNamePrefix { get; }
        public string TimerName { get; }
        public Mock<ITimer> Timer { get; }
        public List<ITimer> Timers { get; }
        public Mock<IComparer<ITimer>> TimerComparer { get; }
        public DateTimeOffset? Now { get; set; }

        public TimerExecutionService Target
        {
            get
            {
                var serviceClientFactory = _fixture.GetServiceClientFactory(_output.GetLoggerFactory());
                var timeProvider = new Mock<TimeProvider>();
                timeProvider.Setup(x => x.GetUtcNow()).Returns(() => Now ?? DateTimeOffset.UtcNow);
                return new TimerExecutionService(
                    Timers,
                    _fixture.GetLeaseService(serviceClientFactory, _output.GetLoggerFactory()),
                    new SpecificTimerExecutionService(
                        serviceClientFactory,
                        TimerComparer.Object,
                        timeProvider.Object,
                        _fixture.Options.Object,
                        _output.GetTelemetryClient(),
                        _output.GetLogger<SpecificTimerExecutionService>()),
                    _output.GetLogger<TimerExecutionService>());
            }
        }

        protected async Task<IReadOnlyList<T>> GetEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            var table = await _fixture.GetTableAsync(_output.GetLoggerFactory());
            return await table
                .QueryAsync<T>(x => x.PartitionKey == TimerExecutionService.PartitionKey
                                 && string.Compare(x.RowKey, TimerNamePrefix, StringComparison.Ordinal) >= 0
                                 && string.Compare(x.RowKey, TimerNamePrefix + char.MaxValue, StringComparison.Ordinal) < 0)
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
            private bool _created;

            public Fixture()
            {
                Options = new Mock<IOptions<NuGetInsightsSettings>>();
                Settings = new NuGetInsightsSettings
                {
                    TimerTableName = LogicTestSettings.NewStoragePrefix() + "1t1",
                    LeaseContainerName = LogicTestSettings.NewStoragePrefix() + "1l1",
                }.WithTestStorageSettings();
                Options.Setup(x => x.Value).Returns(() => Settings);
            }

            public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
            public NuGetInsightsSettings Settings { get; }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public async Task<TableClientWithRetryContext> GetTableAsync(ILoggerFactory loggerFactory)
            {
                var serviceClientFactory = GetServiceClientFactory(loggerFactory);
                var table = (await serviceClientFactory.GetTableServiceClientAsync(Options.Object.Value))
                    .GetTableClient(Options.Object.Value.TimerTableName);

                if (!_created)
                {
                    await GetLeaseService(serviceClientFactory, loggerFactory).InitializeAsync();
                    await table.CreateIfNotExistsAsync(retry: true);
                    _created = true;
                }

                return table;
            }

            public AutoRenewingStorageLeaseService GetLeaseService(
                ServiceClientFactory serviceClientFactory,
                ILoggerFactory loggerFactory)
            {
                return new AutoRenewingStorageLeaseService(
                    new StorageLeaseService(serviceClientFactory, loggerFactory.GetLoggerTelemetryClient(), Options.Object),
                    loggerFactory.CreateLogger<AutoRenewingStorageLeaseService>());
            }

            public ServiceClientFactory GetServiceClientFactory(ILoggerFactory loggerFactory)
            {
                return new ServiceClientFactory(Options.Object, loggerFactory.GetLoggerTelemetryClient(), loggerFactory);
            }

            public async Task DisposeAsync()
            {
                var serviceClient = await GetServiceClientFactory(NullLoggerFactory.Instance).GetBlobServiceClientAsync(Options.Object.Value);
                await serviceClient
                    .GetBlobContainerClient(Options.Object.Value.LeaseContainerName)
                    .DeleteIfExistsAsync();

                var table = await GetTableAsync(NullLoggerFactory.Instance);
                await table.DeleteAsync();
            }
        }
    }
}
