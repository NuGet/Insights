// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker
{
    public abstract class BaseCatalogScanToCsvIntegrationTest<T> : BaseCatalogScanIntegrationTest where T : ICsvRecord
    {
        public BaseCatalogScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName { get; }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield return DestinationContainerName;
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            await AssertCompactAsync<T>(DestinationContainerName, testName, stepName, bucket);
        }

        protected async Task AssertBlobCountAsync(int expected)
        {
            await AssertBlobCountAsync(DestinationContainerName, expected);
        }
    }

    public abstract class BaseCatalogScanToCsvIntegrationTest<T1, T2> : BaseCatalogScanIntegrationTest
        where T1 : ICsvRecord
        where T2 : ICsvRecord
    {
        public BaseCatalogScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName1 { get; }
        protected abstract string DestinationContainerName2 { get; }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield return DestinationContainerName1;
            yield return DestinationContainerName2;
        }

        protected async Task AssertBlobCountAsync(int expected)
        {
            await AssertBlobCountAsync(DestinationContainerName1, expected);
            await AssertBlobCountAsync(DestinationContainerName2, expected);
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            await AssertOutputT1Async(testName, stepName, bucket);
            await AssertOutputT2Async(testName, stepName, bucket);
        }

        protected async Task AssertOutputT1Async(string testName, string stepName, int bucket, string fileName = null)
        {
            await AssertCompactAsync<T1>(DestinationContainerName1, testName, Path.Combine(stepName, "T1"), bucket, fileName);
        }

        protected async Task AssertOutputT2Async(string testName, string stepName, int bucket, string fileName = null)
        {
            await AssertCompactAsync<T2>(DestinationContainerName2, testName, Path.Combine(stepName, "T2"), bucket, fileName);
        }
    }


    public abstract class BaseCatalogScanToCsvIntegrationTest<T1, T2, T3> : BaseCatalogScanIntegrationTest
        where T1 : ICsvRecord
        where T2 : ICsvRecord
        where T3 : ICsvRecord
    {
        public BaseCatalogScanToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected abstract string DestinationContainerName1 { get; }
        protected abstract string DestinationContainerName2 { get; }
        protected abstract string DestinationContainerName3 { get; }

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            yield return DestinationContainerName1;
            yield return DestinationContainerName2;
            yield return DestinationContainerName3;
        }

        protected async Task AssertOutputAsync(string testName, string stepName, int bucket)
        {
            await AssertOutputT1Async(testName, stepName, bucket);
            await AssertOutputT2Async(testName, stepName, bucket);
            await AssertOutputT3Async(testName, stepName, bucket);
        }

        protected async Task AssertBlobCountAsync(int expected)
        {
            await AssertBlobCountAsync(DestinationContainerName1, expected);
            await AssertBlobCountAsync(DestinationContainerName2, expected);
            await AssertBlobCountAsync(DestinationContainerName3, expected);
        }

        protected async Task AssertOutputT1Async(string testName, string stepName, int bucket, string fileName = null)
        {
            await AssertCompactAsync<T1>(DestinationContainerName1, testName, Path.Combine(stepName, "T1"), bucket, fileName);
        }

        protected async Task AssertOutputT2Async(string testName, string stepName, int bucket, string fileName = null)
        {
            await AssertCompactAsync<T2>(DestinationContainerName2, testName, Path.Combine(stepName, "T2"), bucket, fileName);
        }

        protected async Task AssertOutputT3Async(string testName, string stepName, int bucket, string fileName = null)
        {
            await AssertCompactAsync<T3>(DestinationContainerName3, testName, Path.Combine(stepName, "T3"), bucket, fileName);
        }
    }
}
