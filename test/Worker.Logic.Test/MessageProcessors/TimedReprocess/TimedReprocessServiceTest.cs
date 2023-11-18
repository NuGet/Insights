// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessServiceTest : BaseWorkerLogicIntegrationTest
    {
        public class GetReprocessBatches : TimedReprocessServiceTest
        {
            [Fact]
            public void ReprocessBatches()
            {
                var batches = TimedReprocessService.GetReprocessBatches();

                Assert.Equal(2, batches.Count);
                Assert.Equal(
                    new[] { CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.LoadSymbolPackageArchive },
                    batches[0]);
                Assert.Equal(
                    new[] { CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.SymbolPackageArchiveToCsv },
                    batches[1]);
            }

            [Fact]
            public void AllowsDisabledDependent()
            {
                ConfigureWorkerSettings = x => x.DisabledDrivers = [CatalogScanDriverType.PackageReadmeToCsv];

                var batches = TimedReprocessService.GetReprocessBatches();

                Assert.Equal(2, batches.Count);
                Assert.Equal(
                    new[] { CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.LoadSymbolPackageArchive },
                    batches[0]);
                Assert.Equal(
                    new[] { CatalogScanDriverType.SymbolPackageArchiveToCsv },
                    batches[1]);
            }

            [Fact]
            public void AllowsDisabledChain()
            {
                ConfigureWorkerSettings = x => x.DisabledDrivers = [CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.PackageReadmeToCsv];

                var batches = TimedReprocessService.GetReprocessBatches();

                Assert.Equal(2, batches.Count);
                Assert.Equal(
                    new[] { CatalogScanDriverType.LoadSymbolPackageArchive },
                    batches[0]);
                Assert.Equal(
                    new[] { CatalogScanDriverType.SymbolPackageArchiveToCsv },
                    batches[1]);
            }

            [Fact]
            public void RejectsDisabledDependency()
            {
                ConfigureWorkerSettings = x => x.DisabledDrivers = [CatalogScanDriverType.LoadPackageReadme];

                var ex = Assert.Throws<InvalidOperationException>(TimedReprocessService.GetReprocessBatches);
                Assert.Equal(
                    "Check the NuGetInsightsWorkerSettings.DisabledDrivers option. Some drivers are missing dependencies: PackageReadmeToCsv depends on LoadPackageReadme",
                    ex.Message);
            }

            public GetReprocessBatches(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
            }
        }

        public TimedReprocessServiceTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
