// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageVersionToCsv
{
    public class PackageVersionToCsvIntegrationTest : BaseCatalogScanToCsvIntegrationTest<PackageVersionRecord>
    {
        private const string PackageVersionToCsvDir = nameof(PackageVersionToCsv);
        private const string PackageVersionToCsv_WithDeleteDir = nameof(PackageVersionToCsv_WithDelete);
        private const string PackageVersionToCsv_WithDuplicatesDir = nameof(PackageVersionToCsv_WithDuplicates);
        private const string PackageVersionToCsv_WithAllLatestDir = nameof(PackageVersionToCsv_WithAllLatest);

        [Fact]
        public async Task PackageVersionToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageVersionToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageVersionToCsvDir, Step1, 1);
            await AssertOutputAsync(PackageVersionToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max2);
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageVersionToCsvDir, Step2, 0);
            await AssertOutputAsync(PackageVersionToCsvDir, Step2, 1);
            await AssertOutputAsync(PackageVersionToCsvDir, Step2, 2);
        }

        [Fact]
        public async Task PackageVersionToCsv_WithDelete()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max2);
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageVersionToCsv_WithDeleteDir, Step2, 2);
        }

        [Fact]
        public async Task PackageVersionToCsv_WithDuplicates()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageVersionToCsv_WithDuplicatesDir, Step1, 0);
        }

        [Fact]
        public async Task PackageVersionToCsv_WithAllLatest()
        {
            // Arrange
            ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

            var min0 = DateTimeOffset.Parse("2021-02-28T01:06:32.8546849Z").AddTicks(-1);
            var max1 = DateTimeOffset.Parse("2021-02-28T01:06:32.8546849Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageVersion, min0);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(CatalogScanDriverType.LoadPackageVersion, onlyLatestLeaves: null, max1);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageVersionToCsv_WithAllLatestDir, Step1, 0);
        }

        public PackageVersionToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageVersionToCsv;
        protected override string DestinationContainerName => Options.Value.PackageVersionContainerName;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { CatalogScanDriverType.LoadPackageVersion };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => new[] { DriverType };

        protected override IEnumerable<string> GetExpectedLeaseNames()
        {
            return base.GetExpectedLeaseNames().Concat(new[] { "Start-" + CatalogScanDriverType.LoadPackageVersion });
        }

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageVersion });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.PackageVersionTableName;
        }
    }
}
