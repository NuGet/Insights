// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.LoadLatestPackageLeaf
{
    public class LoadLatestPackageLeafIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string LoadLatestPackageLeavesDir = nameof(LoadLatestPackageLeaf);
        private const string LoadLatestPackageLeaves_WithDuplicatesInCommitDir = nameof(LoadLatestPackageLeaf_WithDuplicatesInCommit);

        [Fact]
        public async Task LoadLatestPackageLeaf()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
            var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadLatestPackageLeavesDir, Step1);
        }

        [Fact]
        public async Task LoadLatestPackageLeaf_WithDuplicatesInCommit()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2018-03-23T08:55:02.1875809Z");
            var max1 = DateTimeOffset.Parse("2018-03-23T08:55:20.0232708Z");
            var max2 = DateTimeOffset.Parse("2018-03-23T08:55:38.0342003Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(LoadLatestPackageLeaves_WithDuplicatesInCommitDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(LoadLatestPackageLeaves_WithDuplicatesInCommitDir, Step2);
        }

        public LoadLatestPackageLeafIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadLatestPackageLeaf;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            yield return Options.Value.LatestPackageLeafTableName;
        }

        private async Task AssertOutputAsync(string dir, string step)
        {
            var table = (await ServiceClientFactory.GetTableServiceClientAsync())
                .GetTableClient(Options.Value.LatestPackageLeafTableName);
            await AssertEntityOutputAsync<LatestPackageLeaf>(table, Path.Combine(dir, step));
        }
    }
}
