// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    public class CatalogDataToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageDeprecationRecord, PackageVulnerabilityRecord>
    {
        private const string CatalogDataToCsv_DeprecationDir = nameof(CatalogDataToCsv_Deprecation);
        private const string CatalogDataToCsv_VulnerabilitiesDir = nameof(CatalogDataToCsv_Vulnerabilities);
        private const string CatalogDataToCsv_WithDeleteDir = nameof(CatalogDataToCsv_WithDelete);

        public class CatalogDataToCsv_Deprecation : CatalogDataToCsvIntegrationTest
        {
            public CatalogDataToCsv_Deprecation(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var max1 = DateTimeOffset.Parse("2020-07-08T17:12:18.5692562Z");
                var min0 = max1.AddTicks(-1);

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertBlobCountAsync(DestinationContainerName1, 3);
                await AssertBlobCountAsync(DestinationContainerName2, 3);
                await AssertOutputAsync(CatalogDataToCsv_DeprecationDir, Step1, 0);
                await AssertOutputAsync(CatalogDataToCsv_DeprecationDir, Step1, 1);
                await AssertOutputAsync(CatalogDataToCsv_DeprecationDir, Step1, 2);
            }
        }
        public class CatalogDataToCsv_Vulnerabilities : CatalogDataToCsvIntegrationTest
        {
            public CatalogDataToCsv_Vulnerabilities(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var max1 = DateTimeOffset.Parse("2021-07-01T21:45:03.3861285Z");
                var min0 = max1.AddTicks(-1);

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertBlobCountAsync(DestinationContainerName1, 3);
                await AssertBlobCountAsync(DestinationContainerName2, 3);
                await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 0);
                await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 1);
                await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 2);
            }
        }

        public class CatalogDataToCsv_WithDelete : CatalogDataToCsvIntegrationTest
        {
            public CatalogDataToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step2, 2);
            }
        }

        public CatalogDataToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName1 => Options.Value.PackageDeprecationContainerName;
        protected override string DestinationContainerName2 => Options.Value.PackageVulnerabilityContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.CatalogDataToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();
    }
}
