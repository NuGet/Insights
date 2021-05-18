// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageManifestToCsv
{
    public class PackageManifestToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageManifestRecord>
    {
        private const string PackageManifestToCsvDir = nameof(PackageManifestToCsv);
        private const string PackageManifestToCsv_WithDeleteDir = nameof(PackageManifestToCsv_WithDelete);

        public class PackageManifestToCsv : PackageManifestToCsvIntegrationTest
        {
            public PackageManifestToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z");
                var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageManifestToCsvDir, Step1, 0);
                await AssertOutputAsync(PackageManifestToCsvDir, Step1, 1);
                await AssertOutputAsync(PackageManifestToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageManifestToCsvDir, Step2, 0);
                await AssertOutputAsync(PackageManifestToCsvDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageManifestToCsvDir, Step2, 2);
            }
        }

        public class PackageManifestToCsv_WithDelete : PackageManifestToCsvIntegrationTest
        {
            public PackageManifestToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                MakeDeletedPackageAvailable();
                var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
                var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
                var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageManifest, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(PackageManifestToCsv_WithDeleteDir, Step2, 2);
            }
        }

        public PackageManifestToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageManifestContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageManifestToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageManifest });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageManifestTableName });
        }
    }
}
