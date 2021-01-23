using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntityServiceTest : IClassFixture<WideEntityServiceTest.Fixture>
    {
        [Fact]
        public async Task ReturnsNullForNonExistentEntity()
        {
            // Arrange
            var partitionKey = nameof(ReturnsNullForNonExistentEntity);
            var rowKey = "foo";

            // Act
            var wideEntity = await Target.GetAsync(TableName, partitionKey, rowKey);

            // Assert
            Assert.Null(wideEntity);
        }

        [Theory]
        [MemberData(nameof(RoundTripsBytesTestData))]
        public async Task RoundTripsBytes(int length)
        {
            // Arrange
            var src = Bytes.Slice(0, length);
            var partitionKey = nameof(RoundTripsBytes) + length;
            var rowKey = "foo";

            // Act
            await Target.InsertAsync(TableName, partitionKey, rowKey, src);
            var wideEntity = await Target.GetAsync(TableName, partitionKey, rowKey);

            // Assert
            using var srcStream = wideEntity.GetStream();
            using var destStream = new MemoryStream();
            await srcStream.CopyToAsync(destStream);

            Assert.Equal(src.ToArray(), destStream.ToArray());
        }

        [Fact]
        public async Task PopulatesWideEntityProperties()
        {
            // Arrange
            var src = Bytes.Slice(0, WideEntityService.MaxTotalEntitySize);
            var partitionKey = nameof(PopulatesWideEntityProperties);
            var rowKey = "foo";

            // Act
            var before = DateTimeOffset.UtcNow;
            await Target.InsertAsync(TableName, partitionKey, rowKey, src);
            var after = DateTimeOffset.UtcNow;
            var wideEntity = await Target.GetAsync(TableName, partitionKey, rowKey);

            // Assert
            Assert.Equal(partitionKey, wideEntity.PartitionKey);
            Assert.Equal(rowKey, wideEntity.RowKey);
            var error = TimeSpan.FromMinutes(5);
            Assert.InRange(wideEntity.Timestamp, before.Subtract(error), after.Add(error));
            Assert.NotEqual(default, wideEntity.Timestamp);
            Assert.NotNull(wideEntity.ETag);
            Assert.Equal(_fixture.IsLoopback ? 8 : 3, wideEntity.SegmentCount);
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
                while (current <= WideEntityService.MaxTotalEntitySize);

                for (var i = 16; i >= 0; i--)
                {
                    yield return WideEntityService.MaxTotalEntitySize;
                }

                var random = new Random(0);
                for (var i = 0; i < 26; i++)
                {
                    yield return random.Next(0, WideEntityService.MaxTotalEntitySize);
                }
            }
        }

        private readonly Fixture _fixture;

        public WideEntityServiceTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            Target = new WideEntityService(_fixture.ServiceClientFactory);
        }

        public string TableName => _fixture.TableName;
        public ReadOnlyMemory<byte> Bytes => _fixture.Bytes.AsMemory();
        public WideEntityService Target { get; }

        public class Fixture : IAsyncLifetime
        {
            public Fixture()
            {
                Options = new Mock<IOptions<ExplorePackagesSettings>>();
                Settings = new ExplorePackagesSettings();
                Options.Setup(x => x.Value).Returns(() => Settings);
                ServiceClientFactory = new ServiceClientFactory(Options.Object);
                TableName = "t" + StorageUtility.GenerateUniqueId().ToLowerInvariant();
                IsLoopback = GetTable().Uri.IsLoopback;

                Bytes = new byte[4 * 1024 * 1024];
                var random = new Random(0);
                random.NextBytes(Bytes);
            }

            public Mock<IOptions<ExplorePackagesSettings>> Options { get; }
            public ExplorePackagesSettings Settings { get; }
            public ServiceClientFactory ServiceClientFactory { get; }
            public string TableName { get; }
            public bool IsLoopback { get; }
            public byte[] Bytes { get; }

            public Task InitializeAsync()
            {
                return GetTable().CreateIfNotExistsAsync();
            }

            public Task DisposeAsync()
            {
                return GetTable().DeleteIfExistsAsync();
            }

            private CloudTable GetTable()
            {
                return ServiceClientFactory.GetStorageAccount().CreateCloudTableClient().GetTableReference(TableName);
            }
        }
    }
}
