// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.Worker.LoadBucketedPackage;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessStorageServiceTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task InitializesBuckets()
        {
            // Act & Act
            var beforeCreated = DateTimeOffset.UtcNow;
            await Target.InitializeAsync();
            var afterCreated = DateTimeOffset.UtcNow;
            var buckets = await Target.GetBucketsAsync();

            // Assert
            Assert.Equal(BucketedPackage.BucketCount, buckets.Count);
            Assert.Equal(Enumerable.Range(0, BucketedPackage.BucketCount), buckets.Select(x => x.Index));
            Assert.All(buckets, x => Assert.InRange(x.LastProcessed, beforeCreated - (2 * Options.Value.TimedReprocessWindow), afterCreated - (2 * Options.Value.TimedReprocessWindow)));
        }

        [Fact]
        public async Task ReturnsMaxBucketCountFirstTime()
        {
            // Act
            await Target.InitializeAsync();

            // Act
            var buckets = await Target.GetBucketsToReprocessAsync();

            // Assert
            var originalBuckets = (await Target.GetBucketsAsync()).ToDictionary(b => b.Index);
            Assert.Equal(Options.Value.TimedReprocessMaxBuckets, buckets.Count);
            Assert.All(buckets, x => Assert.Equal(originalBuckets[x.Index].ETag, x.ETag));
            Assert.All(buckets, x => Assert.Equal(originalBuckets[x.Index].LastProcessed + Options.Value.TimedReprocessWindow, x.LastProcessed));
        }

        [Fact]
        public async Task CanReturnAllBucketsAsync()
        {
            // Act
            await Target.InitializeAsync();
            var batches = new List<List<Bucket>>();

            // Act
            var totalCount = 0;
            do
            {
                var buckets = await Target.GetBucketsToReprocessAsync();
                Output.WriteLine(string.Join(" ", buckets.Select(b => b.Index.ToString("D3"))));
                batches.Add(buckets);
                totalCount += buckets.Count;
                await Target.MarkBucketsAsProcessedAsync(buckets);
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

        public TimedReprocessStorageService Target => Host.Services.GetRequiredService<TimedReprocessStorageService>();

        public TimedReprocessStorageServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
