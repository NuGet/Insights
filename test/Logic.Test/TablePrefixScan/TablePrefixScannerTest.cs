// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.StorageNoOpRetry;

namespace NuGet.Insights.TablePrefixScan
{
    public class TablePrefixScannerTest : IClassFixture<TablePrefixScannerTest.Fixture>
    {
        [Fact]
        public async Task AllowsProjection()
        {
            (var table, var expected) = await _fixture.SortAndInsertAsync(GenerateTestEntities("PK", 2, 2));
            MinSelectColumns = new[] { StorageUtility.PartitionKey, StorageUtility.RowKey, nameof(TestEntity.FieldB) };

            var actual = await Target.ListAsync<TestEntity>(table, string.Empty, MinSelectColumns, takeCount: 1);

            Assert.Equal(expected.Count, actual.Count);
            Assert.All(expected.Zip(actual), (pair) =>
            {
                // Built-in properties that are returned automatically
                Assert.Equal(pair.First.ETag, pair.Second.ETag);

                // Built-in properties that are required
                Assert.Equal(pair.First.PartitionKey, pair.Second.PartitionKey);
                Assert.Equal(pair.First.RowKey, pair.Second.RowKey);

                // Custom properties
                Assert.Null(pair.Second.FieldA);
                Assert.Equal(pair.First.FieldB, pair.Second.FieldB);
            });
        }

        [Theory]
        [InlineData(1, 1, 1, "[AA] [AB] [AC] [AD] [AE] [AF] [AG] [AH] [BI] [BJ] [BK] [BL] [BM] [BN] [BO] [BP]")]
        [InlineData(1, 2, 1, "[AA, AB] [AC, AD] [AE] [AF] [AG] [AH] [BI] [BJ, BK] [BL] [BM] [BN] [BO] [BP]")]
        [InlineData(1, 2, 3, "[AA, AB] [AC, AD] [AE, AF, AG] [AH] [BI, BJ, BK] [BL, BM] [BN, BO, BP]")]
        [InlineData(1, 3, 2, "[AA, AB, AC] [AD, AE, AF] [AG, AH] [BI, BJ] [BK, BL, BM] [BN, BO] [BP]")]
        [InlineData(2, 1, 3, "[AA, AB] [AC, AD] [AE, AF, AG, AH] [BI, BJ, BK, BL, BM, BN] [BO, BP]")]
        [InlineData(2, 3, 1, "[AA, AB, AC, AD, AE, AF] [AG, AH] [BI, BJ] [BK, BL, BM, BN, BO, BP]")]
        [InlineData(3, 1, 2, "[AA, AB, AC] [AD, AE, AF] [AG, AH] [BI, BJ, BK, BL, BM, BN] [BO, BP]")]
        [InlineData(3, 2, 1, "[AA, AB, AC, AD, AE, AF] [AG, AH] [BI, BJ, BK] [BL, BM, BN, BO, BP]")]
        public async Task AllowsMultipleSegmentsPerPrefix(int takeCount, int segmentsPerFirstPrefix, int segmentsPerSubsequentPrefix, string expected)
        {
            (var table, var all) = await _fixture.SortAndInsertAsync(new[]
            {
                new TestEntity("AA", "1"),
                new TestEntity("AB", "1"),
                new TestEntity("AC", "1"),
                new TestEntity("AD", "1"),
                new TestEntity("AE", "1"),
                new TestEntity("AF", "1"),
                new TestEntity("AG", "1"),
                new TestEntity("AH", "1"),
                new TestEntity("BI", "1"),
                new TestEntity("BJ", "1"),
                new TestEntity("BK", "1"),
                new TestEntity("BL", "1"),
                new TestEntity("BM", "1"),
                new TestEntity("BN", "1"),
                new TestEntity("BO", "1"),
                new TestEntity("BP", "1"),
            });

            var segments = await Target.ListSegmentsAsync<TestEntity>(
                table,
                string.Empty,
                MinSelectColumns,
                takeCount,
                expandPartitionKeys: true,
                segmentsPerFirstPrefix,
                segmentsPerSubsequentPrefix);

            var actual = string.Join(" ", segments.Select(x => "[" + string.Join(", ", x.Select(x => x.PartitionKey)) + "]"));
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("")]
        [InlineData("P")]
        public async Task EnumeratesEmptyTable(string prefix)
        {
            (var table, var _) = await _fixture.SortAndInsertAsync(Enumerable.Empty<TestEntity>());

            var actual = await Target.ListAsync<TestEntity>(table, prefix, MinSelectColumns, StorageUtility.MaxTakeCount);

            Assert.Empty(actual);
        }

        [Theory]
        [InlineData("")]
        [InlineData("P")]
        public async Task HandlesSurrogatePairs(string prefix)
        {
            // For some reason the Azure Storage Emulator sorts the '𐐷' character before 'A'. Real Azure Table Storage
            // does not do this.
            var hackPrefix = LogicTestSettings.IsStorageEmulator ? "A" : string.Empty;

            (var table, var expected) = await _fixture.SortAndInsertAsync(new[]
            {
                new TestEntity(prefix + "B", "R1"),
                new TestEntity(prefix + "C", "R1"),
                new TestEntity(prefix + hackPrefix + "𐐷", "R1"),
                new TestEntity(prefix + hackPrefix + "𐐷", "R2"),
                new TestEntity(prefix + hackPrefix + "𐐷", "R3"),
                new TestEntity(prefix + "𤭢a", "R1"),
                new TestEntity(prefix + "𤭢b", "R2"),
                new TestEntity(prefix + "𤭢c", "R3"),
                new TestEntity(prefix + "𤭣", "R1"),
                new TestEntity(prefix + "𤭣", "R2"),
                new TestEntity(prefix + "𤭣", "R3"),
            });

            var actual = await Target.ListAsync<TestEntity>(table, prefix, MinSelectColumns, takeCount: 1);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(EnumeratesEmptyPrefixTestData))]
        public async Task EnumeratesEmptyPrefix(string prefix, int takeCount)
        {
            (var table, var _) = await _fixture.SortAndInsertAsync(new[]
            {
                new TestEntity("B", "R1"),
                new TestEntity("D", "R1"),
            });

            var actual = await Target.ListAsync<TestEntity>(table, prefix, MinSelectColumns, takeCount);

            Assert.Empty(actual);
        }

        public static IEnumerable<object[]> EnumeratesEmptyPrefixTestData
        {
            get
            {
                foreach (var prefix in new[] { "A", "C", "E" })
                {
                    foreach (var takeCount in new[] { 1, 2, 1000 })
                    {
                        yield return new object[] { prefix, takeCount };
                    }
                }
            }
        }

        [Fact]
        public async Task EnumeratesAllEntitiesInSinglePage()
        {
            (var table, var expected) = await _fixture.SortAndInsertAsync(GenerateTestEntities("PK", 3, 2));

            var actual = await Target.ListAsync<TestEntity>(table, string.Empty, MinSelectColumns, StorageUtility.MaxTakeCount);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task EnumeratesAllEntitiesWithNoPrefix(int takeCount)
        {
            (var table, var expected) = await _fixture.SortAndInsertAsync(GenerateTestEntities("PK", 3, 3));

            var actual = await Target.ListAsync<TestEntity>(table, string.Empty, MinSelectColumns, takeCount);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task EnumeratesAllEntitiesWithPrefixMatchingEverything(int takeCount)
        {
            (var table, var expected) = await _fixture.SortAndInsertAsync(GenerateTestEntities("PK", 3, 3));

            var actual = await Target.ListAsync<TestEntity>(table, "P", MinSelectColumns, takeCount);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task EnumeratesAllEntitiesWithMatchingPrefix(int takeCount)
        {
            (var table, var all) = await _fixture.SortAndInsertAsync(Enumerable
                .Empty<TestEntity>()
                .Concat(GenerateTestEntities("AK", 3, 3))
                .Concat(GenerateTestEntities("PK", 3, 3))
                .Concat(GenerateTestEntities("ZK", 3, 3)));
            var expected = all.Where(x => x.PartitionKey.StartsWith("PK", StringComparison.Ordinal)).ToList();

            var actual = await Target.ListAsync<TestEntity>(table, "P", MinSelectColumns, takeCount);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task AllowsNotExpandingPartitionKeys()
        {
            (var table, var all) = await _fixture.SortAndInsertAsync(GenerateTestEntities("P", 8, 3));
            var expected = Enumerable
                .Empty<TestEntity>()
                .Concat(all.Take(4))
                .Concat(all.Skip(6).Take(4))
                .Concat(all.Skip(12).Take(4))
                .Concat(all.Skip(18).Take(4));

            var actual = await Target.ListAsync<TestEntity>(
                table,
                partitionKeyPrefix: string.Empty,
                selectColumns: null,
                takeCount: 4,
                expandPartitionKeys: false,
                segmentsPerFirstPrefix: 1,
                segmentsPerSubsequentPrefix: 1);

            Assert.Equal(
                expected.ToArray(),
                actual.ToArray());
            Assert.Equal(
                all.Select(x => x.PartitionKey).Distinct().ToList(),
                actual.Select(x => x.PartitionKey).Distinct().ToList());
        }

        public static TheoryData<string, string, string, string, string, int, int> HandlesUpperAndLowerBoundsTestData
        {
            get
            {
                var data = new TheoryData<string, string, string, string, string, int, int>();

                foreach (var takeCount in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 1000 })
                {
                    data.Add("", "___", "000", null, null, 0, takeCount);
                    data.Add("", "000", "___", "AK1/R1", "ZK1/R1", 23, takeCount);
                    data.Add("", "BB", "CC", null, null, 0, takeCount);
                    data.Add("", null, "PPPP", "/R1", "PPPN/R1", 16, takeCount);
                    data.Add("A", "PPPP", "ZZZZ", null, null, 0, takeCount);
                    data.Add("AK1", "-", "_", "AK1/R1", "AK1/R1", 1, takeCount);
                    data.Add("AK1", "-", "~", "AK1/R1", "AK1/R1", 1, takeCount);
                    data.Add("AK1", "-", null, "AK1/R1", "AK1/R1", 1, takeCount);
                    data.Add("AK1", null, "_", "AK1/R1", "AK1/R1", 1, takeCount);
                    data.Add("AK1", null, "~", "AK1/R1", "AK1/R1", 1, takeCount);
                    data.Add("P", "PA", "PN", "PM/R1", "PM/R1", 1, takeCount);
                    data.Add("P", "PA", "PPPP", "PM/R1", "PPPN/R1", 12, takeCount);
                    data.Add("P", "PPAAAAAAAAAA", null, "PPM/R1", "PZ/R1", 15, takeCount);
                    data.Add("P", null, "PPPP", "P/R1", "PPPN/R1", 14, takeCount);
                    data.Add("PP", "A", null, "PP/R1", "PPZ/R1", 16, takeCount);
                    data.Add("PPP", "AAAA", "ZZZZ", "PPP/R1", "PPPZ/R1", 11, takeCount);
                    data.Add("PPPP", "PPPP", "PPPPPPP", "PPPPP/R1", "PPPPPP/R1", 2, takeCount);
                    data.Add("PPPP", "PPPPA", "PPPPPPP", "PPPPP/R1", "PPPPPP/R1", 2, takeCount);
                    data.Add("PPPP", "PPPPP", "PPPPPPP", "PPPPPP/R1", "PPPPPP/R1", 1, takeCount);
                    data.Add("PPPP", "PPPPPP", "PPPPPPP", null, null, 0, takeCount);
                }

                return data;
            }
        }

        [Theory]
        [MemberData(nameof(HandlesUpperAndLowerBoundsTestData))]
        public async Task HandlesUpperAndLowerBounds(string prefix, string lowerBound, string upperBound, string first, string last, int expectedCount, int takeCount)
        {
            (var table, var all) = await _fixture.SortAndInsertAsync(Enumerable
                .Empty<TestEntity>()
                .Concat(GenerateTestEntities("AK", 1, 1))
                .Concat(new[]
                {
                    new TestEntity("", "R1"),
                    new TestEntity("P", "R1"),
                    new TestEntity("PA", "R1"),
                    new TestEntity("PM", "R1"),
                    new TestEntity("PN", "R1"),
                    new TestEntity("PP", "R1"),
                    new TestEntity("PPA", "R1"),
                    new TestEntity("PPM", "R1"),
                    new TestEntity("PPN", "R1"),
                    new TestEntity("PPP", "R1"),
                    new TestEntity("PPP", "R2"),
                    new TestEntity("PPP", "R3"),
                    new TestEntity("PPPA", "R1"),
                    new TestEntity("PPPM", "R1"),
                    new TestEntity("PPPN", "R1"),
                    new TestEntity("PPPP", "R1"),
                    new TestEntity("PPPPP", "R1"),
                    new TestEntity("PPPPPP", "R1"),
                    new TestEntity("PPPPPPP", "R1"),
                    new TestEntity("PPPZ", "R1"),
                    new TestEntity("PPZ", "R1"),
                    new TestEntity("PZ", "R1"),
                })
                .Concat(GenerateTestEntities("ZK", 1, 1)));
            var expected = all.Where(x =>
                x.PartitionKey.StartsWith(prefix, StringComparison.Ordinal)
                && (lowerBound is null || string.CompareOrdinal(x.PartitionKey, lowerBound) > 0)
                && (upperBound is null || string.CompareOrdinal(x.PartitionKey, upperBound) < 0)).ToList();

            var actual = await Target.ListAsync<TestEntity>(
                table,
                prefix,
                lowerBound,
                upperBound,
                MinSelectColumns,
                takeCount);

            Assert.Equal(expectedCount, actual.Count);
            if (expectedCount > 0)
            {
                Assert.Equal(first, actual.First().ToString());
                Assert.Equal(last, actual.Last().ToString());
            }
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(IncludesPartitionKeyMatchingPrefixTestData))]
        public async Task IncludesPartitionKeyMatchingPrefix(int prefixLength, int expectedCount, int takeCount)
        {
            (var table, var all) = await _fixture.SortAndInsertAsync(Enumerable
                .Empty<TestEntity>()
                .Concat(GenerateTestEntities("AK", 1, 1))
                .Concat(new[]
                {
                    new TestEntity("P", "R1"),
                    new TestEntity("PA", "R1"),
                    new TestEntity("PP", "R1"),
                    new TestEntity("PPA", "R1"),
                    new TestEntity("PPP", "R1"),
                    new TestEntity("PPP", "R2"),
                    new TestEntity("PPP", "R3"),
                    new TestEntity("PPPA", "R1"),
                    new TestEntity("PPPP", "R1"),
                    new TestEntity("PPPPP", "R1"),
                    new TestEntity("PPPPPP", "R1"),
                    new TestEntity("PPPPPPP", "R1"),
                    new TestEntity("PPPZ", "R1"),
                    new TestEntity("PPZ", "R1"),
                    new TestEntity("PZ", "R1"),
                })
                .Concat(GenerateTestEntities("ZK", 1, 1)));
            var prefix = new string('P', prefixLength);
            var expected = all.Where(x => x.PartitionKey.StartsWith(prefix, StringComparison.Ordinal)).ToList();

            var actual = await Target.ListAsync<TestEntity>(table, prefix, MinSelectColumns, takeCount);

            Assert.Equal(expected, actual);
            Assert.Equal(expectedCount, actual.Count);
        }

        public static IEnumerable<object[]> IncludesPartitionKeyMatchingPrefixTestData
        {
            get
            {
                var testData = new List<(int prefixLength, int expectedCount)>
                {
                    (0, 17),
                    (1, 15),
                    (2, 12),
                    (3, 9),
                    (4, 4),
                    (5, 3),
                    (6, 2),
                    (7, 1),
                    (8, 0),
                    (9, 0),
                };
                foreach (var testCase in testData)
                {
                    foreach (var takeCount in new[] { 1, 2, 1000 })
                    {
                        yield return new object[] { testCase.prefixLength, testCase.expectedCount, takeCount };
                    }
                }
            }
        }

        private readonly Fixture _fixture;

        public TablePrefixScannerTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            MinSelectColumns = null;
            Target = new TablePrefixScanner(
                output.GetTelemetryClient(),
                output.GetLogger<TablePrefixScanner>());
        }

        private static IEnumerable<TestEntity> GenerateTestEntities(string prefix, int partitionKeyCount, int rowsPerPartitionKey)
        {
            for (var pk = 1; pk <= partitionKeyCount; pk++)
            {
                for (var rk = 1; rk <= rowsPerPartitionKey; rk++)
                {
                    yield return new TestEntity(prefix + pk, "R" + rk);
                }
            }
        }

        public IList<string> MinSelectColumns { get; set; }
        public TablePrefixScanner Target { get; }

        public class Fixture : IAsyncLifetime
        {
            private static readonly List<(IReadOnlyList<TestEntity> sortedEntities, TableClientWithRetryContext table)> Candidates = new();

            public Fixture()
            {
            }

            public async Task<(TableClientWithRetryContext table, IReadOnlyList<TestEntity> sortedEntities)> SortAndInsertAsync(IEnumerable<TestEntity> entities)
            {
                IReadOnlyList<TestEntity> sortedEntities = entities.Order().ToList();

                TableClientWithRetryContext table = null;
                foreach (var candidate in Candidates)
                {
                    if (candidate.sortedEntities.SequenceEqual(sortedEntities, new PartitionKeyRowKeyComparer<TestEntity>()))
                    {
                        table = candidate.table;
                        sortedEntities = candidate.sortedEntities;
                        break;
                    }
                }

                if (table == null)
                {
                    var options = new Mock<IOptions<NuGetInsightsSettings>>();
                    options
                        .SetupGet(x => x.Value)
                        .Returns(new NuGetInsightsSettings().WithTestStorageSettings());

                    var loggerFactory = new Mock<ILoggerFactory>();
                    loggerFactory
                        .Setup(x => x.CreateLogger(It.IsAny<string>()))
                        .Returns(() => NullLogger.Instance);

                    var serviceClientFactory = new ServiceClientFactory(
                        options.Object,
                        loggerFactory.Object);

                    var client = await serviceClientFactory.GetTableServiceClientAsync();

                    table = client.GetTableClient(LogicTestSettings.NewStoragePrefix() + "1ts1");
                    await table.CreateIfNotExistsAsync();

                    foreach (var group in sortedEntities.GroupBy(x => x.PartitionKey))
                    {
                        var batch = new MutableTableTransactionalBatch(table);
                        batch.AddEntities(group);
                        await batch.SubmitBatchAsync();
                    }
                    Candidates.Add((sortedEntities, table));
                }

                return (table, sortedEntities);
            }

            public Task InitializeAsync()
            {
                return Task.CompletedTask;
            }

            public Task DisposeAsync()
            {
                return Task.WhenAll(Candidates.Select(x => x.table.DeleteAsync()));
            }
        }

        public class PartitionKeyRowKeyComparer<T> : IEqualityComparer<T> where T : ITableEntityWithClientRequestId
        {
            public bool Equals([AllowNull] T x, [AllowNull] T y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return x.PartitionKey == y.PartitionKey && x.RowKey == y.RowKey;
            }

            public int GetHashCode([DisallowNull] T obj)
            {
                return HashCode.Combine(obj.PartitionKey, obj.RowKey);
            }
        }
    }
}
