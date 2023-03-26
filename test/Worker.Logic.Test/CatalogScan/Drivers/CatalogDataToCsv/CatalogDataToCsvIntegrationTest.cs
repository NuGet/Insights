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
    public class CatalogDataToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageDeprecationRecord, PackageVulnerabilityRecord, CatalogLeafItemRecord>
    {
        private const string CatalogDataToCsvDir = nameof(CatalogDataToCsv);
        private const string CatalogDataToCsv_DeprecationDir = nameof(CatalogDataToCsv_Deprecation);
        private const string CatalogDataToCsv_VulnerabilitiesDir = nameof(CatalogDataToCsv_Vulnerabilities);
        private const string CatalogDataToCsv_WithDuplicatesDir = nameof(CatalogDataToCsv_WithDuplicates);
        private const string CatalogDataToCsv_WithDeleteDir = nameof(CatalogDataToCsv_WithDelete);
        private const string CatalogDataToCsv_WithSpecialK_NLSDir = nameof(CatalogDataToCsv_WithSpecialK_NLS);
        private const string CatalogDataToCsv_WithSpecialK_ICUDir = nameof(CatalogDataToCsv_WithSpecialK_ICU);

        public class CatalogDataToCsv : CatalogDataToCsvIntegrationTest
        {
            public CatalogDataToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
                var max1 = DateTimeOffset.Parse("2020-12-27T05:07:45.7628472Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(CatalogDataToCsvDir, Step1, 0);
            }
        }

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
                await AssertBlobCountAsync(DestinationContainerName3, 3);
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
                await AssertBlobCountAsync(DestinationContainerName3, 3);
                await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 0);
                await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 1);
                await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 2);
            }
        }

        public class CatalogDataToCsv_WithDuplicates : CatalogDataToCsvIntegrationTest
        {
            public CatalogDataToCsv_WithDuplicates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                // Arrange
                var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z");
                var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(CatalogDataToCsv_WithDuplicatesDir, Step1, 0);
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

        public class CatalogDataToCsv_WithSpecialK_NLS : CatalogDataToCsv_WithSpecialK
        {
            public CatalogDataToCsv_WithSpecialK_NLS(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [OSPlatformFact(OSPlatformType.Windows)]
            public async Task Execute()
            {
                await RunTestAsync(CatalogDataToCsv_WithSpecialK_NLSDir);
            }
        }

        public class CatalogDataToCsv_WithSpecialK_ICU : CatalogDataToCsv_WithSpecialK
        {
            public CatalogDataToCsv_WithSpecialK_ICU(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [OSPlatformFact(OSPlatformType.Linux | OSPlatformType.OSX | OSPlatformType.FreeBSD)]
            public async Task Execute()
            {
                await RunTestAsync(CatalogDataToCsv_WithSpecialK_ICUDir);
            }
        }

        public abstract class CatalogDataToCsv_WithSpecialK : CatalogDataToCsvIntegrationTest
        {
            public CatalogDataToCsv_WithSpecialK(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            protected async Task RunTestAsync(string dir)
            {
                ConfigureWorkerSettings = x => x.AppendResultStorageBucketCount = 1;

                // Arrange
                var min0 = DateTimeOffset.Parse("2021-08-11T23:38:05.65091Z");
                var max1 = DateTimeOffset.Parse("2021-08-11T23:39:31.9024782Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(dir, Step1, 0);
            }
        }

        public CatalogDataToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName1 => Options.Value.PackageDeprecationContainerName;
        protected override string DestinationContainerName2 => Options.Value.PackageVulnerabilityContainerName;
        protected override string DestinationContainerName3 => Options.Value.CatalogLeafItemContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.CatalogDataToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();
    }
}
