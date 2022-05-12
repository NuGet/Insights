// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    public class SymbolPackageArchiveToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>
    {
        private const string SymbolPackageArchiveToCsvDir = nameof(SymbolPackageArchiveToCsv);
        private const string SymbolPackageArchiveToCsv_WithDeleteDir = nameof(SymbolPackageArchiveToCsv_WithDelete);

        public class SymbolPackageArchiveToCsv : SymbolPackageArchiveToCsvIntegrationTest
        {
            public SymbolPackageArchiveToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2021-03-22T20:13:00.3409860Z");
                var max1 = DateTimeOffset.Parse("2021-03-22T20:13:54.6075418Z");
                var max2 = DateTimeOffset.Parse("2021-03-22T20:15:23.6403188Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(SymbolPackageArchiveToCsvDir, Step1, 0);
                await AssertBlobCountAsync(1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(SymbolPackageArchiveToCsvDir, Step2, 0);
                await AssertOutputAsync(SymbolPackageArchiveToCsvDir, Step2, 2);
                await AssertBlobCountAsync(2);
            }
        }

        public class SymbolPackageArchiveToCsv_WithDelete : SymbolPackageArchiveToCsvIntegrationTest
        {
            public SymbolPackageArchiveToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(CatalogScanDriverType.LoadSymbolPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertOutputAsync(SymbolPackageArchiveToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputAsync(SymbolPackageArchiveToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputAsync(SymbolPackageArchiveToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertOutputAsync(SymbolPackageArchiveToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputAsync(SymbolPackageArchiveToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputAsync(SymbolPackageArchiveToCsv_WithDeleteDir, Step2, 2);
            }
        }

        public SymbolPackageArchiveToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override string DestinationContainerName1 => Options.Value.SymbolPackageArchiveContainerName;
        protected override string DestinationContainerName2 => Options.Value.SymbolPackageArchiveEntryContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.SymbolPackageArchiveToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[]
            {
                "CatalogScan-" + CatalogScanDriverType.LoadSymbolPackageArchive,
            });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[]
            {
                Options.Value.SymbolPackageArchiveTableName,
            });
        }
    }
}
