// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.LoadSymbolPackageArchive
{
    public class LoadSymbolPackageArchiveIntegrationTest : BaseCatalogScanIntegrationTest
    {
        public const string LoadSymbolPackageArchiveDir = nameof(LoadSymbolPackageArchive);
        public const string LoadSymbolPackageArchive_WithDeleteDir = nameof(LoadSymbolPackageArchive_WithDelete);

        public class LoadSymbolPackageArchive : LoadSymbolPackageArchiveIntegrationTest
        {
            public LoadSymbolPackageArchive(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertSymbolPackageArchiveOutputAsync(LoadSymbolPackageArchiveDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertSymbolPackageArchiveOutputAsync(LoadSymbolPackageArchiveDir, Step2);
            }
        }

        public class LoadSymbolPackageArchive_WithDelete : LoadSymbolPackageArchiveIntegrationTest
        {
            public LoadSymbolPackageArchive_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await AssertSymbolPackageArchiveOutputAsync(LoadSymbolPackageArchive_WithDeleteDir, Step1);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertSymbolPackageArchiveOutputAsync(LoadSymbolPackageArchive_WithDeleteDir, Step2);
            }
        }

        public LoadSymbolPackageArchiveIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.LoadSymbolPackageArchive;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[] { Options.Value.SymbolPackageArchiveTableName });
        }
    }
}
