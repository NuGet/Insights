// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.TimedReprocess;
using NuGet.Insights.Worker.Workflow;
#if ENABLE_CRYPTOAPI
using NuGet.Insights.Worker.PackageCertificateToCsv;
using NuGet.Insights.Worker.ReferenceTracking;
#endif

namespace NuGet.Insights.Worker
{
    public class TimerComparerTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public void TimersAreInProperOrder()
        {
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

#if ENABLE_CRYPTOAPI
                new List<Type>
                {
                    typeof(CleanupOrphanRecordsTimer<CertificateRecord>),
                },
#endif

                new List<Type>
                {
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<ExcludedPackage>>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<PackageDownloads>>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<PackageOwner>>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<PopularityTransfer>>),
                    typeof(AuxiliaryFileUpdaterTimer<AsOfData<VerifiedPackage>>),
                },

                new List<Type>
                {
                    typeof(KustoIngestionTimer),
                },
            };

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
