// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessStorageServiceIntegrationTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task InitializesBuckets()
        {
            // Act & Act
            UtcNow = new DateTimeOffset(2024, 9, 5, 20, 45, 0, TimeSpan.Zero);
            await TimedReprocessStorageService.InitializeAsync();
            var buckets = await TimedReprocessStorageService.GetBucketsAsync();

            // Assert
            Assert.Equal(BucketedPackage.BucketCount, buckets.Count);
            Assert.Equal(Enumerable.Range(0, BucketedPackage.BucketCount), buckets.Select(x => x.Index));
            Assert.All(buckets, x => Assert.Null(x.LastProcessed));
            Assert.All(buckets, x => Assert.InRange(x.ScheduledFor, new DateTimeOffset(2024, 9, 2, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2024, 9, 16, 0, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public async Task CanCountAllStaleBucketsFirstTime()
        {
            // Act
            await TimedReprocessStorageService.InitializeAsync();

            // Act
            var buckets = await TimedReprocessStorageService.GetAllStaleBucketsAsync();

            // Assert
            Assert.Equal(Enumerable.Range(0, BucketedPackage.BucketCount), buckets);
        }

        [Fact]
        public async Task ReturnsZeroStaleWhenAllHaveBeenProcessed()
        {
            // Arrange
            await TimedReprocessStorageService.InitializeAsync();
            var allBuckets = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();
            UtcNow = DateTimeOffset.UtcNow + 2 * Options.Value.TimedReprocessWindow;
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(allBuckets);
            UtcNow = null;

            // Act
            var buckets = await TimedReprocessStorageService.GetAllStaleBucketsAsync();

            // Assert
            Assert.Empty(buckets);
        }

        [Fact]
        public async Task ReturnsMaxBucketCountFirstTime()
        {
            // Arrange
            await TimedReprocessStorageService.InitializeAsync();

            // Act
            var buckets = await TimedReprocessStorageService.GetBucketsToReprocessAsync();

            // Assert
            var originalBuckets = (await TimedReprocessStorageService.GetBucketsAsync()).ToDictionary(b => b.Index);
            Assert.Equal(Options.Value.TimedReprocessMaxBuckets, buckets.Count);
            Assert.All(buckets, x => Assert.Equal(originalBuckets[x.Index].LastProcessed, x.LastProcessed));
            Assert.All(buckets, x => Assert.Equal(originalBuckets[x.Index].ETag, x.ETag));
        }

        [Fact]
        public async Task CanUpdateLastProcessedToRecent()
        {
            // Arrange
            await TimedReprocessStorageService.InitializeAsync();
            var buckets = await TimedReprocessStorageService.GetBucketsToReprocessAsync();
            var now = DateTimeOffset.UtcNow;

            // Act
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets.Select(x => x.Index));

            // Assert
            var originalBuckets = (await TimedReprocessStorageService.GetBucketsAsync()).ToDictionary(b => b.Index);
            Assert.Equal(Options.Value.TimedReprocessMaxBuckets, buckets.Count);
            Assert.All(buckets, x => Assert.True(originalBuckets[x.Index].LastProcessed > now));
            Assert.All(buckets, x => Assert.NotEqual(originalBuckets[x.Index].ETag, x.ETag));
        }

        [Fact]
        public async Task CanReturnAllBucketsAsync()
        {
            // Arrange
            await TimedReprocessStorageService.InitializeAsync();
            var batches = new List<List<TimedReprocessBucket>>();

            // Act
            var totalCount = 0;
            do
            {
                var buckets = await TimedReprocessStorageService.GetBucketsToReprocessAsync();
                Output.WriteLine(string.Join(" ", buckets.Select(b => b.Index.ToString("D3", CultureInfo.InvariantCulture))));
                batches.Add(buckets);
                Assert.NotEmpty(buckets);
                totalCount += buckets.Count;
                await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets.Select(x => x.Index));
            }
            while (totalCount < BucketedPackage.BucketCount);

            // Assert
            Assert.Equal(
                Enumerable.Range(0, BucketedPackage.BucketCount),
                batches.SelectMany(b => b).Select(b => b.Index).Distinct().OrderBy(x => x));
            Assert.Equal(
                Enumerable.Range(0, BucketedPackage.BucketCount),
                batches.SelectMany(b => b).Select(b => b.Index));
        }

        [Fact]
        public async Task DoesNotReturnDuplicatesWhenBehind()
        {
            // Arrange
            UtcNow = new DateTimeOffset(2024, 9, 5, 20, 45, 0, TimeSpan.Zero);
            await TimedReprocessStorageService.InitializeAsync();
            var subset = Enumerable.Range(0, Options.Value.TimedReprocessMaxBuckets / 2).Select(i => 10 + i * 3).ToList();
            var others = Enumerable.Range(0, BucketedPackage.BucketCount).Except(subset).ToList();
            UtcNow += Options.Value.TimedReprocessWindow;
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(subset);
            UtcNow += 2 * Options.Value.TimedReprocessWindow;
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(others);

            // Act
            var buckets = await TimedReprocessStorageService.GetBucketsToReprocessAsync();

            // Assert
            Assert.Equal(subset, buckets.Select(x => x.Index));
        }

        [Theory]
        [InlineData(6, 17, 18)]
        [InlineData(12, 35, 36)]
        [InlineData(16, 47, 48)]
        public async Task ReturnsAllBucketsDuringTheTimeWindow(int hourJump, int minBucketsPerBatch, int maxBucketsPerBatch)
        {
            // Arrange
            var frequencyTimeSpan = TimeSpan.FromHours(hourJump);
            ConfigureWorkerSettings = x =>
            {
                x.TimedReprocessWindow = TimeSpan.FromDays(14); // 1 bucket per ~20 minutes (14 days * 24 hours per day * 60 minutes per hour / 1000 buckets)
                x.TimedReprocessMaxBuckets = 50;
                x.TimedReprocessFrequency = frequencyTimeSpan.ToString(); // at least once every 16 hours is fast enough (14 days * (24 hour per day / 16 hours) * 50 buckets = 1050)
            };

            UtcNow = new DateTimeOffset(2024, 9, 1, 23, 59, 0, TimeSpan.Zero);
            var windowEnd = new DateTimeOffset(2024, 9, 16, 0, 0, 0, TimeSpan.Zero);
            // next window: [2024-09-02T00:00:00, 2024-09-16T00:00:00)

            await TimedReprocessStorageService.InitializeAsync();
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(Enumerable.Range(0, BucketedPackage.BucketCount));
            UtcNow += TimeSpan.FromMinutes(2);

            // Act
            var batches = new List<List<int>>();

            while (UtcNow < windowEnd)
            {
                UtcNow += frequencyTimeSpan;

                var buckets = (await TimedReprocessStorageService.GetBucketsToReprocessAsync()).Select(x => x.Index).ToList();
                batches.Add(buckets);
                if (buckets.Count > 0)
                {
                    await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets);
                }
            }

            // Assert
            var expectedBuckets = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();
            Assert.InRange(batches.Sum(x => x.Count), BucketedPackage.BucketCount, BucketedPackage.BucketCount + 50);
            var actualBuckets = batches.SelectMany(x => x).Take(BucketedPackage.BucketCount).ToList(); // skip extra couple buckets at the end
            Assert.Equal(expectedBuckets, actualBuckets);
            Assert.All(batches, x => Assert.InRange(x.Count, minBucketsPerBatch, maxBucketsPerBatch));
        }

        [Fact]
        public async Task ReturnsAllBucketsDuringMultipleTimeWindows()
        {
            // Arrange
            var frequencyTimeSpan = TimeSpan.FromHours(24);
            ConfigureWorkerSettings = x =>
            {
                x.TimedReprocessWindow = TimeSpan.FromDays(14);
                x.TimedReprocessMaxBuckets = 100;
                x.TimedReprocessFrequency = frequencyTimeSpan.ToString(); // at least once every 24 hours is plenty fast enough to keep up (14 days * (24 hour per day / 24 hours) * 150 buckets = 1400)
            };

            UtcNow = new DateTimeOffset(2024, 9, 1, 23, 59, 0, TimeSpan.Zero);
            var windowStart = new DateTimeOffset(2024, 9, 2, 0, 0, 0, TimeSpan.Zero);
            var iterations = 10;
            var lastWindowEnd = windowStart + TimeSpan.FromDays(14 * iterations);

            await TimedReprocessStorageService.InitializeAsync();
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(Enumerable.Range(0, BucketedPackage.BucketCount));
            UtcNow += TimeSpan.FromMinutes(2);

            // Act
            var batches = new List<List<int>>();

            while (UtcNow < lastWindowEnd)
            {
                UtcNow += frequencyTimeSpan;

                var buckets = (await TimedReprocessStorageService.GetBucketsToReprocessAsync()).Select(x => x.Index).ToList();
                batches.Add(buckets);
                if (buckets.Count > 0)
                {
                    await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets);
                }
            }

            // Assert
            var bucketToHits = batches.SelectMany(x => x).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
            Assert.Equal(BucketedPackage.BucketCount, bucketToHits.Count);
            var missingCoverage = bucketToHits.Where(x => x.Value < iterations).ToList();
            Assert.Empty(missingCoverage.Select(x => $"{x.Key} ({x.Value}x)").ToList());
            Assert.All(bucketToHits, x => Assert.InRange(x.Value, iterations, iterations + 1));
            Assert.All(batches, x => Assert.InRange(x.Count, 71, 72));
        }

        [Fact]
        public async Task CatchesUpWithVeryOldTimestamps()
        {
            // Arrange
            var frequencyTimeSpan = TimeSpan.FromHours(24);
            ConfigureWorkerSettings = x =>
            {
                x.TimedReprocessWindow = TimeSpan.FromDays(14);
                x.TimedReprocessMaxBuckets = 100;
                x.TimedReprocessFrequency = frequencyTimeSpan.ToString(); // at least once every 24 hours is plenty fast enough to keep up (14 days * (24 hour per day / 24 hours) * 150 buckets = 1400)
            };

            UtcNow = new DateTimeOffset(2024, 9, 1, 23, 59, 0, TimeSpan.Zero);

            await TimedReprocessStorageService.InitializeAsync();
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(Enumerable.Range(0, BucketedPackage.BucketCount));
            UtcNow += 10 * Options.Value.TimedReprocessWindow + TimeSpan.FromMinutes(2);
            var currentTimeWindow = TimedReprocessStorageService.GetCurrentTimeWindow();
            var nextTimeWindow = currentTimeWindow.GetNext();

            // Act
            var catchUpBatches = new List<List<int>>();
            while (UtcNow + frequencyTimeSpan < currentTimeWindow.WindowEnd)
            {
                UtcNow += frequencyTimeSpan;

                var buckets = (await TimedReprocessStorageService.GetBucketsToReprocessAsync()).Select(x => x.Index).ToList();
                catchUpBatches.Add(buckets);
                if (buckets.Count > 0)
                {
                    await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets);
                }
            }

            // we should be caught up now, with no more buckets to process
            var shouldBeEmptyA = await TimedReprocessStorageService.GetBucketsToReprocessAsync();

            var scheduledBatches = new List<List<int>>();
            while (UtcNow < nextTimeWindow.WindowEnd)
            {
                UtcNow += frequencyTimeSpan;

                var buckets = (await TimedReprocessStorageService.GetBucketsToReprocessAsync()).Select(x => x.Index).ToList();
                scheduledBatches.Add(buckets);
                if (buckets.Count > 0)
                {
                    await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets);
                }
            }

            // we should be caught up now, with no more buckets to process
            var shouldBeEmptyB = await TimedReprocessStorageService.GetBucketsToReprocessAsync();

            // Assert
            Assert.Empty(shouldBeEmptyA);
            Assert.Empty(shouldBeEmptyB);

            Assert.Equal(Enumerable.Range(0, BucketedPackage.BucketCount), catchUpBatches.SelectMany(x => x));
            Assert.All(catchUpBatches.Where(x => x.Count > 0), x => Assert.Equal(100, x.Count));

            Assert.Equal(Enumerable.Range(0, BucketedPackage.BucketCount).Append(0), scheduledBatches.SelectMany(x => x));
            Assert.Single(scheduledBatches, x => x.Count == 1);
            Assert.All(scheduledBatches.Where(x => x.Count > 1), x => Assert.InRange(x.Count, 71, 72));
        }

        public TimedReprocessStorageServiceIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
