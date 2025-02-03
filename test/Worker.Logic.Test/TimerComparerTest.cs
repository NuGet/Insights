// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.DownloadsToCsv;
using NuGet.Insights.Worker.ExcludedPackagesToCsv;
using NuGet.Insights.Worker.GitHubUsageToCsv;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.OwnersToCsv;
using NuGet.Insights.Worker.PopularityTransfersToCsv;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.TimedReprocess;
using NuGet.Insights.Worker.VerifiedPackagesToCsv;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class TimerComparerTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public void TimersAreInProperOrder()
        {
            var cleanupOrphanRecordsTimers = typeof(CleanupOrphanRecordsTimer<>).Assembly
                .GetTypes()
                .Where(x => x.IsAssignableTo(typeof(ICleanupOrphanCsvRecord)))
                .Where(x => x.IsClass && !x.IsAbstract)
                .OrderBy(x => x.FullName)
                .Select(x => typeof(CleanupOrphanRecordsTimer<>).MakeGenericType(x))
                .ToList();

            var expectedGroups = new List<List<Type>>
            {
                new List<Type>
                {
                    typeof(WorkflowTimer),
                },

                new List<Type>
                {
                    typeof(TimedReprocessTimer),
                },

                new List<Type>
                {
                    typeof(CatalogScanUpdateTimer),
                },

                cleanupOrphanRecordsTimers,

                new List<Type>
                {
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<ExcludedPackage>, ExcludedPackageRecord>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<GitHubRepositoryInfo>, GitHubUsageRecord>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<PackageDownloads>, PackageDownloadRecord>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<PackageOwner>, PackageOwnerRecord>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<PopularityTransfer>, PopularityTransfersRecord>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<VerifiedPackage>, VerifiedPackageRecord>),
                },

                new List<Type>
                {
                    typeof(KustoIngestionTimer),
                },
            };

            expectedGroups.RemoveAll(x => x.Count == 0);

            var timers = Host.Services.GetServices<ITimer>();
            var actualGroups = SpecificTimerExecutionService
                .GroupAndOrder(timers, x => x, TimerComparer.Instance)
                .ToList();

            Assert.Equal(expectedGroups.Count, actualGroups.Count);
            Assert.All(expectedGroups.Zip(actualGroups), tuple =>
            {
                var (expectedGroup, actualGroup) = tuple;
                Assert.Equal(expectedGroup.Count, actualGroup.Count);
                Assert.All(expectedGroup.Zip(actualGroup.OrderBy(t => t.GetType().FullName)), tuple =>
                {
                    var (expected, actual) = tuple;
                    Assert.IsType(expected, actual);
                });
            });
        }

        public TimerComparerTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
