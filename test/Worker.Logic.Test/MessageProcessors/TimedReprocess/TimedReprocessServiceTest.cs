// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NuGet.Insights.Worker.TimedReprocess
{
    public class TimedReprocessServiceTest
    {
        [Fact]
        public void ReprocessBatches()
        {
            Assert.Equal(2, TimedReprocessService.ReprocessBatches.Count);
            Assert.Equal(
                new[] { CatalogScanDriverType.LoadPackageReadme, CatalogScanDriverType.LoadSymbolPackageArchive },
                TimedReprocessService.ReprocessBatches[0]);
            Assert.Equal(
                new[] { CatalogScanDriverType.PackageReadmeToCsv, CatalogScanDriverType.SymbolPackageArchiveToCsv },
                TimedReprocessService.ReprocessBatches[1]);
        }

        [Theory]
        [MemberData(nameof(DriverTypeTestData))]
        public void IsDriverExpectedForReprocess(CatalogScanDriverType driverType)
        {
            var expected = new HashSet<CatalogScanDriverType>
            {
                CatalogScanDriverType.LoadPackageReadme,
                CatalogScanDriverType.PackageReadmeToCsv,

                CatalogScanDriverType.LoadSymbolPackageArchive,
                CatalogScanDriverType.SymbolPackageArchiveToCsv,
            };

            var reprocess = TimedReprocessService.ShouldDriverBeReprocessed(driverType);

            Assert.Equal(expected.Contains(driverType), reprocess);
        }

        public static IEnumerable<object[]> DriverTypeTestData => CatalogScanCursorService
            .StartableDriverTypes
            .Select(x => new object[] { x });
    }
}
