// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageContentToCsv
{
    public class PackageContentToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageContent>
    {
        private const string PackageContentToCsvDir = nameof(PackageContentToCsv);
        private const string PackageContentToCsv_WithDeleteDir = nameof(PackageContentToCsv_WithDelete);

        [Fact]
        public async Task PackageContentToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2022-03-14T23:05:39.6122305Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2022-03-14T23:06:07.7549588Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2022-03-14T23:06:36.1633247Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageContentToCsvDir, Step1, 0);
            await AssertOutputAsync(PackageContentToCsvDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageContentToCsvDir, Step1, 0); // This file is unchanged
            await AssertOutputAsync(PackageContentToCsvDir, Step2, 2);
            await AssertBlobCountAsync(DestinationContainerName, 2);
        }

        [Fact]
        public async Task PackageContentToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(PackageContentToCsv_WithDeleteDir, Step1, 0);
            await AssertOutputAsync(PackageContentToCsv_WithDeleteDir, Step1, 1);
            await AssertOutputAsync(PackageContentToCsv_WithDeleteDir, Step1, 2);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(PackageContentToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
            await AssertOutputAsync(PackageContentToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
            await AssertOutputAsync(PackageContentToCsv_WithDeleteDir, Step2, 2);
        }

        public PackageContentToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName => Options.Value.PackageContentContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageContentToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageArchiveTableName });
        }
    }
}
