// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Insights.ReferenceTracking;
using NuGet.Insights.Worker.ReferenceTracking;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.PackageCertificateToCsv
{
    public class PackageCertificateToCsvIntegrationTest : BaseCatalogLeafScanToCsvIntegrationTest<PackageCertificateRecord, CertificateRecord>
    {
        public const string PackageCertificateToCsvDir = nameof(PackageCertificateToCsv);
        public const string PackageCertificateToCsv_WithManyCertificatesDir = nameof(PackageCertificateToCsv_WithManyCertificates);
        public const string PackageCertificateToCsv_WithDeleteDir = nameof(PackageCertificateToCsv_WithDelete);
        public const string PackageCertificateToCsv_WithSingleDeleteDir = nameof(PackageCertificateToCsv_WithSingleDelete);
        public const string PackageCertificateToCsv_WithDollarSignIdDir = nameof(PackageCertificateToCsv_WithDollarSignId);

        public class PackageCertificateToCsv : PackageCertificateToCsvIntegrationTest
        {
            public PackageCertificateToCsv(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2021-12-09T22:05:17.4250722Z");
                var max1 = DateTimeOffset.Parse("2021-12-09T22:05:41.2080967Z");
                var max2 = DateTimeOffset.Parse("2021-12-09T22:06:08.5695122Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsvDir, Step1);
                await AssertOutputT1Async(PackageCertificateToCsvDir, Step1, 0);
                await AssertOutputT1Async(PackageCertificateToCsvDir, Step1, 1);
                await AssertOutputT1Async(PackageCertificateToCsvDir, Step1, 2);
                await AssertOutputT2Async(PackageCertificateToCsvDir, Step1, 0);
                await AssertOutputT2Async(PackageCertificateToCsvDir, Step1, 1);
                await AssertOutputT2Async(PackageCertificateToCsvDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsvDir, Step2);
                await AssertOutputT1Async(PackageCertificateToCsvDir, Step2, 0);
                await AssertOutputT1Async(PackageCertificateToCsvDir, Step2, 1);
                await AssertOutputT1Async(PackageCertificateToCsvDir, Step2, 2);
                await AssertOutputT2Async(PackageCertificateToCsvDir, Step2, 0);
                await AssertOutputT2Async(PackageCertificateToCsvDir, Step2, 1);
                await AssertOutputT2Async(PackageCertificateToCsvDir, Step2, 2);
            }
        }

        public class PackageCertificateToCsv_WithManyCertificates : PackageCertificateToCsvIntegrationTest
        {
            public PackageCertificateToCsv_WithManyCertificates(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2023-01-17T09:51:59.7223256Z");
                var max1 = DateTimeOffset.Parse("2023-01-17T09:52:47.3352455Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsv_WithManyCertificatesDir, Step1);
                await AssertOutputT1Async(PackageCertificateToCsv_WithManyCertificatesDir, Step1, 0);
                await AssertOutputT1Async(PackageCertificateToCsv_WithManyCertificatesDir, Step1, 1);
                await AssertOutputT1Async(PackageCertificateToCsv_WithManyCertificatesDir, Step1, 2);
                await AssertOutputT2Async(PackageCertificateToCsv_WithManyCertificatesDir, Step1, 0);
                await AssertOutputT2Async(PackageCertificateToCsv_WithManyCertificatesDir, Step1, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithManyCertificatesDir, Step1, 2);
            }
        }

        public class PackageCertificateToCsv_WithDelete : PackageCertificateToCsv_WithCleanupOrphans
        {
            public PackageCertificateToCsv_WithDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
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
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max2);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsv_WithDeleteDir, Step1);
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 2);
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 0);
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 2);

                // Act
                await UpdateAsync(max2);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsv_WithDeleteDir, Step2);
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step2, 2);
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 2); // This file is unchanged.

                // Act
                await CleanupOrphansAsync();

                // Assert
                await AssertOwnerToSubjectAsync(PackageCertificateToCsv_WithDeleteDir, Step2); // This file is unchanged.
                await AssertSubjectToOwnerAsync(PackageCertificateToCsv_WithDeleteDir, Step3);
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputT1Async(PackageCertificateToCsv_WithDeleteDir, Step2, 2); // This file is unchanged.
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 0); // This file is unchanged.
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step3, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithDeleteDir, Step1, 2); // This file is unchanged.
            }
        }

        public class PackageCertificateToCsv_WithSingleDelete : PackageCertificateToCsv_WithCleanupOrphans
        {
            public PackageCertificateToCsv_WithSingleDelete(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                MakeDeletedPackageAvailable(id: "DeltaX", version: "1.0.0");
                var min0 = DateTimeOffset.Parse("2019-02-05T18:40:48.4041109Z");
                var max1 = DateTimeOffset.Parse("2019-02-05T18:42:32.4259826Z");
                var min2 = DateTimeOffset.Parse("2020-04-10T18:16:55.1000051Z");
                var max3 = DateTimeOffset.Parse("2020-04-10T18:18:43.6788949Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max3);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsv_WithSingleDeleteDir, Step1);
                await AssertOutputT1Async(PackageCertificateToCsv_WithSingleDeleteDir, Step1, 1);
                await AssertBlobCountAsync(DestinationContainerName1, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithSingleDeleteDir, Step1, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithSingleDeleteDir, Step1, 2);
                await AssertBlobCountAsync(DestinationContainerName2, 2);

                // Act
                await SetCursorAsync(DriverType, min2);
                await UpdateAsync(max3);

                // Assert
                await AssertOwnerToSubjectAsync(PackageCertificateToCsv_WithSingleDeleteDir, Step2, "empty-array.json");
                await AssertSubjectToOwnerAsync(PackageCertificateToCsv_WithSingleDeleteDir, Step2);
                await AssertOutputT1Async(PackageCertificateToCsv_WithSingleDeleteDir, Step2, 1);
                await AssertBlobCountAsync(DestinationContainerName1, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithSingleDeleteDir, Step1, 1); // This file is unchanged.
                await AssertOutputT2Async(PackageCertificateToCsv_WithSingleDeleteDir, Step1, 2); // This file is unchanged.
                await AssertBlobCountAsync(DestinationContainerName2, 2);

                // Act
                await CleanupOrphansAsync();

                // Assert
                await AssertOwnerToSubjectAsync(PackageCertificateToCsv_WithSingleDeleteDir, Step2, "empty-array.json"); // This file is unchanged.
                await AssertSubjectToOwnerAsync(PackageCertificateToCsv_WithSingleDeleteDir, Step2, "empty-array.json");
                await AssertOutputT1Async(PackageCertificateToCsv_WithSingleDeleteDir, Step2, 1); // This file is unchanged.
                await AssertBlobCountAsync(DestinationContainerName1, 1);
                await AssertOutputT2Async(PackageCertificateToCsv_WithSingleDeleteDir, Step3, 1, "empty.csv");
                await AssertOutputT2Async(PackageCertificateToCsv_WithSingleDeleteDir, Step3, 2, "empty.csv");
                await AssertBlobCountAsync(DestinationContainerName2, 2);
            }
        }

        public class PackageCertificateToCsv_WithDollarSignId : PackageCertificateToCsvIntegrationTest
        {
            public PackageCertificateToCsv_WithDollarSignId(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            [Fact]
            public async Task Execute()
            {
                // Arrange
                var min0 = DateTimeOffset.Parse("2016-03-20T22:08:18.0892014Z");
                var max1 = DateTimeOffset.Parse("2016-03-21T06:41:26.9274378Z");

                await CatalogScanService.InitializeAsync();
                await SetCursorAsync(CatalogScanDriverType.LoadPackageArchive, max1);
                await SetCursorAsync(min0);

                // Act
                await UpdateAsync(max1);

                // Assert
                await AssertTableOutputAsync(PackageCertificateToCsv_WithDollarSignIdDir, Step1, "empty-array.json");
                await AssertOutputT1Async(PackageCertificateToCsv_WithDollarSignIdDir, Step1, 2);
                await AssertBlobCountAsync(DestinationContainerName1, 1);
                await AssertBlobCountAsync(DestinationContainerName2, 0);
            }
        }

        public abstract class PackageCertificateToCsv_WithCleanupOrphans : PackageCertificateToCsvIntegrationTest
        {
            public PackageCertificateToCsv_WithCleanupOrphans(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
                : base(output, factory)
            {
            }

            protected async Task CleanupOrphansAsync()
            {
                var cleanup = Host.Services.GetRequiredService<ICleanupOrphanRecordsService<CertificateRecord>>();
                await cleanup.InitializeAsync();
                await cleanup.StartAsync();
                await ProcessQueueAsync(async () => !await cleanup.IsRunningAsync());
            }

            protected override IEnumerable<string> GetExpectedTableNames()
            {
                return base.GetExpectedTableNames().Concat(new[]
                {
                    Options.Value.TaskStateTableName,
                });
            }

            protected override IEnumerable<string> GetExpectedLeaseNames()
            {
                return base.GetExpectedLeaseNames().Concat(new[]
                {
                    $"CleanupOrphans-{ReferenceTypes.Package}-{ReferenceTypes.Certificate}",
                });
            }
        }

        public PackageCertificateToCsvIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            // These values are regularly changing. Don't record them so test data is stable.
            ConfigureWorkerSettings = x => x.RecordCertificateStatusUpdateTimes = false;
        }

        protected override string DestinationContainerName1 => Options.Value.PackageCertificateContainerName;
        protected override string DestinationContainerName2 => Options.Value.CertificateContainerName;
        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.PackageCertificateToCsv;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => new[] { DriverType };
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();

        protected override IEnumerable<string> GetExpectedCursorNames()
        {
            return base.GetExpectedCursorNames().Concat(new[] { "CatalogScan-" + CatalogScanDriverType.LoadPackageArchive });
        }

        protected override IEnumerable<string> GetExpectedTableNames()
        {
            return base.GetExpectedTableNames().Concat(new[]
            {
                Options.Value.PackageArchiveTableName,
                Options.Value.PackageToCertificateTableName,
                Options.Value.CertificateToPackageTableName,
            });
        }

        private async Task AssertTableOutputAsync(string testName, string stepName, string fileName = null)
        {
            await AssertOwnerToSubjectAsync(testName, stepName, fileName);
            await AssertSubjectToOwnerAsync(testName, stepName, fileName);
        }

        private async Task AssertOwnerToSubjectAsync(string testName, string stepName, string fileName = null)
        {
            await AssertOwnerToSubjectAsync(
                Options.Value.PackageToCertificateTableName,
                testName,
                stepName,
                bytes =>
                {
                    var data = MessagePackSerializer.Deserialize<CertificateRelationshipTypes>(
                        bytes,
                        NuGetInsightsMessagePack.Options);
                    return Enum
                        .GetValues<CertificateRelationshipTypes>()
                        .Except(new[] { CertificateRelationshipTypes.None })
                        .Where(x => data.HasFlag(x))
                        .Select(x => Enum.GetName(x))
                        .ToList();
                },
                fileName);
        }

        private async Task AssertSubjectToOwnerAsync(string testName, string stepName, string fileName = null)
        {
            await AssertSubjectToOwnerAsync(
                Options.Value.CertificateToPackageTableName,
                testName,
                stepName,
                fileName);
        }
    }
}
