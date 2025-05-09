// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    public class CatalogDataToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageDeprecationRecord, PackageVulnerabilityRecord, CatalogLeafItemRecord>
    {
        private const string CatalogDataToCsvDir = nameof(CatalogDataToCsv);
        private const string CatalogDataToCsv_IgnoredPackagesDir = nameof(CatalogDataToCsv_IgnoredPackages);
        private const string CatalogDataToCsv_DeprecationDir = nameof(CatalogDataToCsv_Deprecation);
        private const string CatalogDataToCsv_DuplicateLeafUrlDir = nameof(CatalogDataToCsv_DuplicateLeafUrl);
        private const string CatalogDataToCsv_VulnerabilitiesDir = nameof(CatalogDataToCsv_Vulnerabilities);
        private const string CatalogDataToCsv_NoSignatureFileDir = nameof(CatalogDataToCsv_NoSignatureFile);
        private const string CatalogDataToCsv_WithDuplicatesDir = nameof(CatalogDataToCsv_WithDuplicates);
        private const string CatalogDataToCsv_WithDeleteDir = nameof(CatalogDataToCsv_WithDelete);
        private const string CatalogDataToCsv_WithSpecialK_NLSDir = nameof(CatalogDataToCsv_WithKelvinK_NLS);
        private const string CatalogDataToCsv_WithSpecialK_ICUDir = nameof(CatalogDataToCsv_WithKelvinK_ICU);

        [Fact]
        public async Task CatalogDataToCsv()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-27T05:07:45.7628472Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsvDir, Step1, 0);
        }

        [Fact]
        public async Task CatalogDataToCsv_IgnoredPackages()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2025-04-23T21:18:45.5295392Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2025-04-23T21:22:16.2507724Z", CultureInfo.InvariantCulture);
            ConfigureWorkerSettings = x => x.IgnoredPackages =
                [new IgnoredPackagePattern { IdRegex = @"Milvasoft|[^A-Za-z0-9_\.\-]|FluidSharp", MinTimestamp = min0, MaxTimestamp = max1.AddTicks(-1) }];

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_IgnoredPackagesDir, Step1, 0);
            var apiRequests = HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.Host.EndsWith("nuget.org", StringComparison.OrdinalIgnoreCase));
            Assert.All(apiRequests, r => Assert.DoesNotContain("Milvasoft", r.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
            Assert.All(apiRequests, r => Assert.DoesNotContain("test2.avaloni", r.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task CatalogDataToCsv_Deprecation()
        {
            // Arrange
            var max1 = DateTimeOffset.Parse("2020-07-08T17:12:18.5692562Z", CultureInfo.InvariantCulture);
            var min0 = max1.AddTicks(-1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_DeprecationDir, Step1, 0);
        }

        [Fact]
        public async Task CatalogDataToCsv_DuplicateLeafUrl()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2016-01-15T04:02:36.6883253Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2016-01-15T04:03:06.7654918Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_DuplicateLeafUrlDir, Step1, 0);
        }

        [Fact]
        public async Task CatalogDataToCsv_Vulnerabilities()
        {
            // Arrange
            var max1 = DateTimeOffset.Parse("2021-07-01T21:45:03.3861285Z", CultureInfo.InvariantCulture);
            var min0 = max1.AddTicks(-1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_VulnerabilitiesDir, Step1, 0);
        }

        [Fact]
        public async Task CatalogDataToCsv_NoSignatureFile()
        {
            // Arrange
            var max1 = DateTimeOffset.Parse("2018-07-17T18:53:46.1209756Z", CultureInfo.InvariantCulture);
            var min0 = max1.AddTicks(-1);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_NoSignatureFileDir, Step1, 0);
        }

        [Fact]
        public async Task CatalogDataToCsv_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T21:58:12.5094058Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T22:09:56.3587144Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_WithDuplicatesDir, Step1, 0);
        }

        [Fact]
        public async Task CatalogDataToCsv_WithDelete()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step1, 0);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(CatalogDataToCsv_WithDeleteDir, Step2, 0);
        }

        [OSPlatformFact(OSPlatformType.Windows)]
        public async Task CatalogDataToCsv_WithKelvinK_NLS()
        {
            await RunTestWithKelvinKAsync(CatalogDataToCsv_WithSpecialK_NLSDir);
        }

        // [OSPlatformFact(OSPlatformType.Linux | OSPlatformType.OSX | OSPlatformType.FreeBSD | OSPlatformType.Windows)]
        [OSPlatformFact(OSPlatformType.Linux | OSPlatformType.OSX | OSPlatformType.FreeBSD)]
        public async Task CatalogDataToCsv_WithKelvinK_ICU()
        {
            // Environment.SetEnvironmentVariable("NUGET_INSIGHTS_ALLOW_ICU", "true");
            await RunTestWithKelvinKAsync(CatalogDataToCsv_WithSpecialK_ICUDir);
        }

        protected async Task RunTestWithKelvinKAsync(string dir)
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2021-08-11T23:38:05.65091Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2021-08-11T23:39:31.9024782Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(dir, Step1, 0);
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
