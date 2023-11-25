// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.LoadBucketedPackage;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task InitializesBuckets()
        {
            // Act & Act
            var beforeCreated = DateTimeOffset.UtcNow;
            await TimedReprocessStorageService.InitializeAsync();
            var afterCreated = DateTimeOffset.UtcNow;
            var buckets = await TimedReprocessStorageService.GetBucketsAsync();

            // Assert
            Assert.Equal(BucketedPackage.BucketCount, buckets.Count);
            Assert.Equal(Enumerable.Range(0, BucketedPackage.BucketCount), buckets.Select(x => x.Index));
            Assert.All(buckets, x => Assert.InRange(x.LastProcessed, beforeCreated - (2 * Options.Value.TimedReprocessWindow), afterCreated - (2 * Options.Value.TimedReprocessWindow)));
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
            // Act
            await TimedReprocessStorageService.InitializeAsync();
            var allBuckets = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(allBuckets, 2 * Options.Value.TimedReprocessWindow);

            // Act
            var buckets = await TimedReprocessStorageService.GetAllStaleBucketsAsync();

            // Assert
            Assert.Empty(buckets);
        }

        [Fact]
        public async Task ReturnsMaxBucketCountFirstTime()
        {
            // Act
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
        public async Task CanUpdateLastProcessed()
        {
            // Act
            await TimedReprocessStorageService.InitializeAsync();
            var buckets = await TimedReprocessStorageService.GetBucketsToReprocessAsync();

            // Act
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(buckets.Select(x => x.Index));

            // Assert
            var originalBuckets = (await TimedReprocessStorageService.GetBucketsAsync()).ToDictionary(b => b.Index);
            Assert.Equal(Options.Value.TimedReprocessMaxBuckets, buckets.Count);
            Assert.All(buckets, x => Assert.Equal(originalBuckets[x.Index].LastProcessed, x.LastProcessed + Options.Value.TimedReprocessWindow));
            Assert.All(buckets, x => Assert.NotEqual(originalBuckets[x.Index].ETag, x.ETag));
        }

        [Fact]
        public async Task CanReturnAllBucketsAsync()
        {
            // Act
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
            // Act
            await TimedReprocessStorageService.InitializeAsync();
            var subset = Enumerable.Range(0, Options.Value.TimedReprocessMaxBuckets / 2).Select(i => 10 + i * 3).ToList();
            var others = Enumerable.Range(0, BucketedPackage.BucketCount).ToList();
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(subset, -2 * Options.Value.TimedReprocessWindow);
            await TimedReprocessStorageService.MarkBucketsAsProcessedAsync(others, 2 * Options.Value.TimedReprocessWindow);

            // Act
            var buckets = await TimedReprocessStorageService.GetBucketsToReprocessAsync();

            // Assert
            Assert.Equal(subset, buckets.Select(x => x.Index));
        }

        public TimedReprocessStorageServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
