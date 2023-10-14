// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class BuildVersionSetIntegrationTest : BaseCatalogScanIntegrationTest
    {
        private const string BuildVersionSetDir = nameof(BuildVersionSet);
        private const string BuildVersionSet_WithDeleteDir = nameof(BuildVersionSet_WithDelete);
        private const string BuildVersionSet_WithDuplicatesDir = nameof(BuildVersionSet_WithDuplicates);
        private const string BuildVersionSet_WithUnicodeDuplicatesDir = nameof(BuildVersionSet_WithUnicodeDuplicates);
        private const string BuildVersionSet_WithIdCaseChangeDir = nameof(BuildVersionSet_WithIdCaseChange);

        [Fact]
        public async Task BuildVersionSet()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-27T05:06:30.4180312Z");
            var max1 = DateTimeOffset.Parse("2020-12-27T05:07:21.9968244Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(BuildVersionSetDir, Step1);
        }

        [Fact]
        public async Task BuildVersionSet_WithDelete()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2020-12-20T02:37:31.5269913Z");
            var max1 = DateTimeOffset.Parse("2020-12-20T03:01:57.2082154Z");
            var max2 = DateTimeOffset.Parse("2020-12-20T03:03:53.7885893Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            using (var versionSetHandle0 = await VersionSetService.GetOrNullAsync())
            {
                // Assert
                Assert.Null(versionSetHandle0.Value);
            }


            // Act
            await UpdateAsync(max1);
            using (var versionSetHandle1 = await VersionSetService.GetAsync())
            {
                // Assert
                await AssertOutputAsync(BuildVersionSet_WithDeleteDir, Step1);

                Assert.True(versionSetHandle1.Value.TryGetId("Nut.MediatR.ServiceLike.DependencyInjection", out _));
                Assert.True(versionSetHandle1.Value.TryGetVersion("Nut.MediatR.ServiceLike.DependencyInjection", "0.0.0-PREVIEW.0.44", out _));

                Assert.True(versionSetHandle1.Value.TryGetId("BehaviorSample", out _));
                Assert.True(versionSetHandle1.Value.TryGetVersion("BehaviorSample", "1.0.0", out _));

                Assert.False(versionSetHandle1.Value.TryGetId("doesnotexist", out _));
                Assert.False(versionSetHandle1.Value.TryGetVersion("doesnotexist", "1.0.0", out _));
            }

            // Act
            await UpdateAsync(max2);
            using (var versionSetHandle2 = await VersionSetService.GetAsync())
            {
                // Assert
                await AssertOutputAsync(BuildVersionSet_WithDeleteDir, Step2);

                Assert.True(versionSetHandle2.Value.TryGetId("Nut.MediatR.ServiceLike.DependencyInjection", out _));
                Assert.True(versionSetHandle2.Value.TryGetVersion("Nut.MediatR.ServiceLike.DependencyInjection", "0.0.0-PREVIEW.0.44", out _));

                Assert.True(versionSetHandle2.Value.TryGetId("BehaviorSample", out _));
                Assert.True(versionSetHandle2.Value.TryGetVersion("BehaviorSample", "1.0.0", out _));

                Assert.False(versionSetHandle2.Value.TryGetId("doesnotexist", out _));
                Assert.False(versionSetHandle2.Value.TryGetVersion("doesnotexist", "1.0.0", out _));
            }
        }

        [Fact]
        public async Task BuildVersionSet_WithIdCaseChange()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2018-11-16T04:13:07.3793051Z");
            var max1 = DateTimeOffset.Parse("2018-11-16T04:13:15.2076282Z");
            var max2 = DateTimeOffset.Parse("2018-11-16T04:17:16.6086735Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(BuildVersionSet_WithIdCaseChangeDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(BuildVersionSet_WithIdCaseChangeDir, Step2);
        }

        [Fact]
        public async Task BuildVersionSet_WithDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2019-01-24T15:03:56.0495104Z");
            var max1 = DateTimeOffset.Parse("2019-01-24T21:30:58.7012340Z");
            var max2 = DateTimeOffset.Parse("2019-01-25T01:00:01.5210470Z");

            await CatalogScanService.InitializeAsync();
            await SetCursorAsync(min0);

            // Act
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(BuildVersionSet_WithDuplicatesDir, Step1);

            // Act
            await UpdateAsync(max2);

            // Assert
            await AssertOutputAsync(BuildVersionSet_WithDuplicatesDir, Step2);
        }

        [Fact]
        public async Task BuildVersionSet_WithUnicodeDuplicates()
        {
            // Arrange
            var min0 = DateTimeOffset.Parse("2022-01-30T01:13:58.2460944Z").AddTicks(-1);
            var max1 = min0.AddTicks(1);
            var min2 = DateTimeOffset.Parse("2022-01-30T01:16:40.6957176Z").AddTicks(-1);
            var max3 = min2.AddTicks(1);

            await CatalogScanService.InitializeAsync();

            // Act
            await SetCursorAsync(min0);
            await UpdateAsync(max1);

            // Assert
            await AssertOutputAsync(BuildVersionSet_WithUnicodeDuplicatesDir, Step1);
            using (var versionSetHandle1 = await VersionSetService.GetAsync())
            {
                Assert.True(versionSetHandle1.Value.TryGetId("Cristina-Buarque-Samba-Sensual-Cancoes-De-Noel-Sem-Tost\u00E3o-1-DOWNLOAD-FULL-ALBUM-MP3-ZIP-of", out _));
                Assert.False(versionSetHandle1.Value.TryGetId("Cristina-Buarque-Samba-Sensual-Cancoes-De-Noel-Sem-Tosta\u0303o-1-DOWNLOAD-FULL-ALBUM-MP3-ZIP-of", out _));
                Assert.True(versionSetHandle1.Value.TryGetId("Christian-Bollmann-Herzensges\u00E4nge-Pearls-of-Love-and-Light-DOWNLOAD-FULL-ALBUM-MP3-ZIP-wo", out _));
                Assert.False(versionSetHandle1.Value.TryGetId("Christian-Bollmann-Herzensgesa\u0308nge-Pearls-of-Love-and-Light-DOWNLOAD-FULL-ALBUM-MP3-ZIP-wo", out _));
            }

            // Act
            await SetCursorAsync(min2);
            await UpdateAsync(max3);

            // Assert
            await AssertOutputAsync(BuildVersionSet_WithUnicodeDuplicatesDir, Step2);
            using (var versionSetHandle2 = await VersionSetService.GetAsync())
            {
                Assert.True(versionSetHandle2.Value.TryGetId("Cristina-Buarque-Samba-Sensual-Cancoes-De-Noel-Sem-Tost\u00E3o-1-DOWNLOAD-FULL-ALBUM-MP3-ZIP-of", out _));
                Assert.True(versionSetHandle2.Value.TryGetId("Cristina-Buarque-Samba-Sensual-Cancoes-De-Noel-Sem-Tosta\u0303o-1-DOWNLOAD-FULL-ALBUM-MP3-ZIP-of", out _));
                Assert.True(versionSetHandle2.Value.TryGetId("Christian-Bollmann-Herzensges\u00E4nge-Pearls-of-Love-and-Light-DOWNLOAD-FULL-ALBUM-MP3-ZIP-wo", out _));
                Assert.True(versionSetHandle2.Value.TryGetId("Christian-Bollmann-Herzensgesa\u0308nge-Pearls-of-Love-and-Light-DOWNLOAD-FULL-ALBUM-MP3-ZIP-wo", out _));
            }
        }

        public BuildVersionSetIntegrationTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
        }

        protected override CatalogScanDriverType DriverType => CatalogScanDriverType.BuildVersionSet;
        public override IEnumerable<CatalogScanDriverType> LatestLeavesTypes => Enumerable.Empty<CatalogScanDriverType>();
        public override IEnumerable<CatalogScanDriverType> LatestLeavesPerIdTypes => Enumerable.Empty<CatalogScanDriverType>();
        public VersionSetService VersionSetService => Host.Services.GetRequiredService<VersionSetService>();

        protected override IEnumerable<string> GetExpectedBlobContainerNames()
        {
            return base.GetExpectedBlobContainerNames().Concat(new[] { Options.Value.VersionSetContainerName });
        }

        protected async Task AssertOutputAsync(string testName, string stepName)
        {
            const string blobName = "version-set.dat";
            const string fileName = "data.json";

            var blob = await GetBlobAsync(Options.Value.VersionSetContainerName, blobName);

            using var memoryStream = new MemoryStream();
            await blob.DownloadToAsync(memoryStream);
            var versions = MessagePackSerializer.Deserialize<VersionSetService.Versions<CaseInsensitiveSortedDictionary<CaseInsensitiveSortedDictionary<bool>>>>(
                memoryStream.ToArray(),
                NuGetInsightsMessagePack.Options);
            var actual = SerializeTestJson(versions);

            var testDataFile = Path.Combine(TestData, testName, stepName, fileName);
            if (OverwriteTestData)
            {
                OverwriteTestDataAndCopyToSource(testDataFile, actual);
            }
            var expected = File.ReadAllText(Path.Combine(TestData, testName, stepName, fileName));
            Assert.Equal(expected, actual);
        }
    }
}
