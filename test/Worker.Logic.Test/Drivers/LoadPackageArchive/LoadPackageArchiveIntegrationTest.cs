// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.LoadPackageArchive
{
    public class LoadPackageArchiveIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public const string LoadPackageArchiveDir = nameof(LoadPackageArchive);
        public const string LoadPackageArchive_IgnoredPackagesDir = nameof(LoadPackageArchive_IgnoredPackages);
        public const string LoadPackageArchive_WithDeleteDir = nameof(LoadPackageArchive_WithDelete);
        public const string LoadPackageArchive_WithManyAssembliesDir = nameof(LoadPackageArchive_WithManyAssemblies);
        public const string LoadPackageArchive_WithManyAssembliesWithDeleteDir = nameof(LoadPackageArchive_WithManyAssembliesWithDelete);
        public const string LoadPackageArchive_WithCommonReferenceDir = nameof(LoadPackageArchive_WithCommonReference);

        [Fact]
        public async Task LoadPackageArchive()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchiveDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchiveDir, Step2);
        }

        [Fact]
        public async Task LoadPackageArchive_IgnoredPackages()
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
            await AssertPackageArchiveTableAsync(LoadPackageArchive_IgnoredPackagesDir, Step1);
            var apiRequests = HttpMessageHandlerFactory.Requests.Where(x => x.RequestUri.Host.EndsWith("nuget.org", StringComparison.OrdinalIgnoreCase));
            Assert.All(apiRequests, r => Assert.DoesNotContain("Milvasoft", r.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
            Assert.All(apiRequests, r => Assert.DoesNotContain("test2.avaloni", r.RequestUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task LoadPackageArchive_WithDelete()
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
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithDeleteDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithDeleteDir, Step2);
        }

        [Fact]
        public async Task LoadPackageArchive_WithManyAssemblies()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-11-27T19:34:24.4257168Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-11-27T19:35:06.0046046Z", CultureInfo.InvariantCulture);
            var max2 = DateTimeOffset.Parse("2020-11-27T19:36:50.4909042Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithManyAssembliesDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithManyAssembliesDir, Step2);
        }

        [Fact]
        public async Task LoadPackageArchive_WithManyAssembliesWithDelete()
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
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithManyAssembliesWithDeleteDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithManyAssembliesWithDeleteDir, Step2);
        }

        [Fact]
        public async Task LoadPackageArchive_WithCommonReference()
        {
            // Arrange
            MakeDeletedPackageAvailable();
            var min0 = DateTimeOffset.Parse("2020-05-12T14:57:48.4894728Z", CultureInfo.InvariantCulture);
            var max1 = DateTimeOffset.Parse("2020-05-12T14:58:03.3232189Z", CultureInfo.InvariantCulture);

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertPackageArchiveTableAsync(LoadPackageArchive_WithCommonReferenceDir, Step1);
        }

        public LoadPackageArchiveIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadPackageArchive;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.PackageArchiveTableName });
        }
    }
}
