using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker
{
    public class CatalogLeafScanMessageProcessorTest : BaseWorkerLogicIntegrationTest
    {
        public class BatchDriver : CatalogLeafScanMessageProcessorTest
        {
            [Fact]
            public async Task AllSucceed()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScans(scans);
                await AssertSuccessAsync(result);
            }

            [Fact]
            public async Task SingleTryAgainLater()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                MockBatchDriver
                    .Setup(x => x.ProcessLeavesAsync(It.IsAny<IReadOnlyList<CatalogLeafScan>>()))
                    .Returns<IReadOnlyList<CatalogLeafScan>>(l => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScan>(
                        failed: Array.Empty<CatalogLeafScan>(),
                        tryAgainLater: l.Where(x => x.StorageSuffix == StorageSuffixA).Take(1).ToList(),
                        notBefore: TimeSpan.FromSeconds(30))));

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScans(scans);
                await AssertTryAgainLaterAsync(result, scans[0], TimeSpan.FromSeconds(30));
            }

            [Fact]
            public async Task SingleFailure()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                MockBatchDriver
                    .Setup(x => x.ProcessLeavesAsync(It.IsAny<IReadOnlyList<CatalogLeafScan>>()))
                    .Returns<IReadOnlyList<CatalogLeafScan>>(l => Task.FromResult(new BatchMessageProcessorResult<CatalogLeafScan>(
                        failed: l.Where(x => x.StorageSuffix == StorageSuffixA).Take(1).ToList())));

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScans(scans);
                await AssertFailureAsync(result, scans[0]);
            }

            [Fact]
            public async Task Poison()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType, AttemptCount = 11 },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType, AttemptCount = 11 },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScans(scans.Where(x => x.AttemptCount <= 10).ToList());
                await AssertPoisonAsync(result, scans, messages);
            }

            [Fact]
            public async Task NoMatchingLeaf()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans.Skip(2).ToList());
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScans(scans.Skip(2).ToList());
                await AssertNotMatchingLeaf(result);
            }

            [Fact]
            public async Task SingleWaiting()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType, NextAttempt = DateTimeOffset.UtcNow + TimeSpan.FromDays(7), },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScans(scans.Skip(1).ToList());
                await AssertWaiting(result, scans[0]);
            }

            public BatchDriver(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                MockBatchDriver = new Mock<ICatalogLeafScanBatchDriver>();

                BatchDriverResult = BatchMessageProcessorResult<CatalogLeafScan>.Empty;

                MockDriverFactory
                    .Setup(x => x.CreateBatchDriverOrNull(It.IsAny<CatalogScanDriverType>()))
                    .Returns(() => MockBatchDriver.Object);
                MockBatchDriver
                    .Setup(x => x.ProcessLeavesAsync(It.IsAny<IReadOnlyList<CatalogLeafScan>>()))
                    .ReturnsAsync(() => BatchDriverResult);
            }

            public Mock<ICatalogLeafScanBatchDriver> MockBatchDriver { get; }
            public BatchMessageProcessorResult<CatalogLeafScan> BatchDriverResult { get; set; }

            private void AssertProcessedScans(IReadOnlyList<CatalogLeafScan> scans)
            {
                var batchCount = 0;
                foreach (var batch in scans.GroupBy(x => new { x.StorageSuffix }))
                {
                    batchCount++;
                    MockBatchDriver.Verify(
                        x => x.ProcessLeavesAsync(It.Is<IReadOnlyList<CatalogLeafScan>>(y => y.Select(z => z.LeafId).SequenceEqual(batch.Select(z => z.LeafId)))),
                        Times.Once);
                }

                MockBatchDriver.Verify(
                    x => x.ProcessLeavesAsync(It.IsAny<IReadOnlyList<CatalogLeafScan>>()),
                    Times.Exactly(batchCount));
            }
        }

        public class NonBatchDriver : CatalogLeafScanMessageProcessorTest
        {
            [Fact]
            public async Task AllSucceed()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScansOneByOne(scans);
                await AssertSuccessAsync(result);
            }

            [Fact]
            public async Task SingleTryAgainLater()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                MockNonBatchDriver
                    .Setup(x => x.ProcessLeafAsync(It.Is<CatalogLeafScan>(l => l.LeafId == scans[0].LeafId)))
                    .ReturnsAsync(DriverResult.TryAgainLater());

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScansOneByOne(scans);
                await AssertTryAgainLaterAsync(result, scans[0], TimeSpan.FromMinutes(1));
            }

            [Fact]
            public async Task SingleFailure()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                MockNonBatchDriver
                    .Setup(x => x.ProcessLeafAsync(It.Is<CatalogLeafScan>(l => l.LeafId == scans[0].LeafId)))
                    .ThrowsAsync(new InvalidOperationException("Oops, this scan failed for some reason."));

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScansOneByOne(scans);
                await AssertFailureAsync(result, scans[0]);
            }

            [Fact]
            public async Task Poison()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType, AttemptCount = 11 },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType, AttemptCount = 11 },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScansOneByOne(scans.Where(x => x.AttemptCount <= 10).ToList());
                await AssertPoisonAsync(result, scans, messages);
            }

            [Fact]
            public async Task NoMatchingLeaf()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans.Skip(2).ToList());
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScansOneByOne(scans.Skip(2).ToList());
                await AssertNotMatchingLeaf(result);
            }

            [Fact]
            public async Task SingleWaiting()
            {
                // Arrange
                var scans = new[]
                {
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-1") { ParsedDriverType = DriverType, NextAttempt = DateTimeOffset.UtcNow + TimeSpan.FromDays(7), },
                    new CatalogLeafScan(StorageSuffixA, ScanId, PageId, "li-2") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-3") { ParsedDriverType = DriverType },
                    new CatalogLeafScan(StorageSuffixB, ScanId, PageId, "li-4") { ParsedDriverType = DriverType },
                };
                await InitializeScansAsync(scans);
                var messages = MakeMessages(scans);

                // Act
                var result = await Target.ProcessAsync(messages, dequeueCount: 0);

                // Assert
                AssertProcessedScansOneByOne(scans.Skip(1).ToList());
                await AssertWaiting(result, scans[0]);
            }

            private void AssertProcessedScansOneByOne(IReadOnlyCollection<CatalogLeafScan> scans)
            {
                foreach (var scan in scans)
                {
                    MockNonBatchDriver.Verify(x => x.ProcessLeafAsync(It.Is<CatalogLeafScan>(l => l.LeafId == scan.LeafId)), Times.Once);
                }

                MockNonBatchDriver.Verify(x => x.ProcessLeafAsync(It.IsAny<CatalogLeafScan>()), Times.Exactly(scans.Count));
            }

            public NonBatchDriver(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
            {
                MockNonBatchDriver = new Mock<ICatalogLeafScanNonBatchDriver>();

                NonBatchDriverResult = DriverResult.Success();

                MockDriverFactory
                    .Setup(x => x.CreateNonBatchDriver(It.IsAny<CatalogScanDriverType>()))
                    .Returns(() => MockNonBatchDriver.Object);
                MockNonBatchDriver
                    .Setup(x => x.ProcessLeafAsync(It.IsAny<CatalogLeafScan>()))
                    .ReturnsAsync(() => NonBatchDriverResult);
            }

            public Mock<ICatalogLeafScanNonBatchDriver> MockNonBatchDriver { get; }
            public DriverResult NonBatchDriverResult { get; set; }
        }

        public CatalogLeafScanMessageProcessorTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            MockMessageEnqueuer = new Mock<IMessageEnqueuer>();
            MockDriverFactory = new Mock<ICatalogScanDriverFactory>();

            StorageSuffixA = "ssa";
            StorageSuffixB = "ssb";
            ScanId = "si";
            PageId = "pi";
            DriverType = CatalogScanDriverType.FindPackageAsset;
        }

        private async Task AssertSuccessAsync(BatchMessageProcessorResult<CatalogLeafScanMessage> result)
        {
            Assert.Empty(MockMessageEnqueuer.Invocations);
            Assert.Empty(result.Failed);
            Assert.Empty(result.TryAgainLater);
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixA, ScanId));
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixB, ScanId));
            AssertOnlyInfoLogsOrLess();
        }

        private async Task AssertTryAgainLaterAsync(BatchMessageProcessorResult<CatalogLeafScanMessage> result, CatalogLeafScan scan, TimeSpan expectedNotBefore)
        {
            Assert.Empty(MockMessageEnqueuer.Invocations);
            Assert.Empty(result.Failed);
            (var actualNotBefore, var tryAgainLater) = Assert.Single(result.TryAgainLater);
            Assert.Equal(scan.LeafId, Assert.Single(tryAgainLater).LeafId);
            Assert.Equal(expectedNotBefore, actualNotBefore);
            Assert.Equal(1, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixA, ScanId));
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixB, ScanId));
            var remaining = Assert.Single(await CatalogScanStorageService.GetLeafScansAsync(StorageSuffixA, ScanId, PageId));
            Assert.Equal(scan.LeafId, remaining.LeafId);
            Assert.Equal(0, remaining.AttemptCount);
            Assert.True(remaining.NextAttempt.Value < DateTimeOffset.UtcNow);
        }

        private async Task AssertFailureAsync(BatchMessageProcessorResult<CatalogLeafScanMessage> result, CatalogLeafScan scan)
        {
            Assert.Empty(MockMessageEnqueuer.Invocations);
            Assert.Equal(scan.LeafId, Assert.Single(result.Failed).LeafId);
            Assert.Empty(result.TryAgainLater);
            Assert.Equal(1, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixA, ScanId));
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixB, ScanId));
            var remaining = Assert.Single(await CatalogScanStorageService.GetLeafScansAsync(StorageSuffixA, ScanId, PageId));
            Assert.Equal(scan.LeafId, remaining.LeafId);
            Assert.Equal(1, remaining.AttemptCount);
            Assert.True(remaining.NextAttempt.Value > DateTimeOffset.UtcNow);
        }

        private async Task AssertPoisonAsync(BatchMessageProcessorResult<CatalogLeafScanMessage> result, IEnumerable<CatalogLeafScan> scans, List<CatalogLeafScanMessage> messages)
        {
            Assert.Equal(2, MockMessageEnqueuer.Invocations.Count);
            MockMessageEnqueuer.Verify(
                x => x.EnqueuePoisonAsync(It.Is<IReadOnlyList<CatalogLeafScanMessage>>(y => y.SequenceEqual(new[] { messages[0] }))),
                Times.Once);
            MockMessageEnqueuer.Verify(
                x => x.EnqueuePoisonAsync(It.Is<IReadOnlyList<CatalogLeafScanMessage>>(y => y.SequenceEqual(new[] { messages[1] }))),
                Times.Once);
            Assert.Empty(result.Failed);
            Assert.Empty(result.TryAgainLater);
            Assert.Equal(2, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixA, ScanId));
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixB, ScanId));
        }

        private async Task AssertNotMatchingLeaf(BatchMessageProcessorResult<CatalogLeafScanMessage> result)
        {
            Assert.Empty(MockMessageEnqueuer.Invocations);
            Assert.Empty(result.Failed);
            Assert.Empty(result.TryAgainLater);
            Assert.False(await CatalogScanStorageService.GetLeafScanTable(StorageSuffixA).ExistsAsync());
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixB, ScanId));
        }

        private async Task AssertWaiting(BatchMessageProcessorResult<CatalogLeafScanMessage> result, CatalogLeafScan scan)
        {
            Assert.Empty(MockMessageEnqueuer.Invocations);
            Assert.Empty(result.Failed);
            (var actualNotBefore, var tryAgainLater) = Assert.Single(result.TryAgainLater);
            Assert.Equal(scan.LeafId, Assert.Single(tryAgainLater).LeafId);
            Assert.Equal(TimeSpan.FromMinutes(5), actualNotBefore);
            Assert.Equal(1, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixA, ScanId));
            Assert.Equal(0, await CatalogScanStorageService.GetLeafScanCountLowerBoundAsync(StorageSuffixB, ScanId));
            var remaining = Assert.Single(await CatalogScanStorageService.GetLeafScansAsync(StorageSuffixA, ScanId, PageId));
            Assert.Equal(scan.LeafId, remaining.LeafId);
            Assert.Equal(0, remaining.AttemptCount);
            Assert.Equal(scan.NextAttempt, remaining.NextAttempt);
        }

        private async Task InitializeScansAsync(IReadOnlyList<CatalogLeafScan> scans)
        {
            foreach (var storageSuffix in scans.Select(x => x.StorageSuffix).Distinct())
            {
                await CatalogScanStorageService.InitializeLeafScanTableAsync(storageSuffix);
            }

            await CatalogScanStorageService.InsertAsync(scans);
        }

        private static List<CatalogLeafScanMessage> MakeMessages(IEnumerable<CatalogLeafScan> scans)
        {
            return scans.Select(x => new CatalogLeafScanMessage
            {
                StorageSuffix = x.StorageSuffix,
                ScanId = x.ScanId,
                PageId = x.PageId,
                LeafId = x.LeafId,
            }).ToList();
        }

        protected override void ConfigureHostBuilder(IHostBuilder hostBuilder)
        {
            base.ConfigureHostBuilder(hostBuilder);

            hostBuilder.ConfigureServices(serviceCollection =>
            {
                serviceCollection.AddTransient<CatalogLeafScanMessageProcessor>();
                serviceCollection.AddTransient(s => MockMessageEnqueuer.Object);
                serviceCollection.AddTransient(s => MockDriverFactory.Object);
            });
        }

        public Mock<IMessageEnqueuer> MockMessageEnqueuer { get; }
        public Mock<ICatalogScanDriverFactory> MockDriverFactory { get; }

        public string StorageSuffixA { get; }
        public string StorageSuffixB { get; }
        public string ScanId { get; }
        public string PageId { get; }
        public CatalogScanDriverType DriverType { get; }

        public CatalogLeafScanMessageProcessor Target => Host.Services.GetRequiredService<CatalogLeafScanMessageProcessor>();
    }
}
