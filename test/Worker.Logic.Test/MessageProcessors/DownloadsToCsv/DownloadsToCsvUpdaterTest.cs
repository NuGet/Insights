// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.BuildVersionSet;

namespace NuGet.Insights.Worker.DownloadsToCsv
{
    public class DownloadsToCsvUpdaterTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public async Task UsesIdCasingFromVersionSet()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("knapcode.TORSHARP", "1.0.1", 456));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,456,456",
                });
        }

        [Fact]
        public async Task UsesVersionCasingFromVersionSet()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1-BETA", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "1.0.1-beta", 456));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1-beta,Knapcode.TorSharp,1.0.1-BETA,456,456",
                });
        }

        [Fact]
        public async Task IgnoresInvalidVersions()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "1.0.1", 456),
                new PackageDownloads("Knapcode.TorSharp", "zzz", 789));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,456,456",
                });
        }

        [Fact]
        public async Task NormalizesVersions()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "1.0.1.0", 456));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,456,456",
                });
        }

        [Fact]
        public async Task WritesIdThatHasNoKnownVersions()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "9.9.9", 456));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,0,0",
                });
        }

        [Fact]
        public async Task WritesExtraVersionInVersionSet()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false),
                ("Knapcode.TorSharp", "1.0.2", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "1.0.2", 456));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.2,Knapcode.TorSharp,1.0.2,456,456",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,0,456",
                });
        }

        [Fact]
        public async Task WritesExtraIdInVersionSet()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false),
                ("Knapcode.TorSharp", "1.0.2", false));
            var asOfData = GetAsOfData(new PackageDownloads("Newtonsoft.Json", "9.0.1", 123));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,0,0",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.2,Knapcode.TorSharp,1.0.2,0,0",
                });
        }

        [Fact]
        public async Task SkipsVersionNotInVersionSet()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "1.0.1", 456),
                new PackageDownloads("Knapcode.TorSharp", "1.0.2", 789));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,456,456",
                });
        }

        [Fact]
        public async Task IncludesDeletedVersion()
        {
            // Arrange
            var versionSet = GetVersionSet(
                ("Newtonsoft.Json", "9.0.1", false),
                ("Knapcode.TorSharp", "1.0.1", false),
                ("Knapcode.TorSharp", "1.0.2", true));
            var asOfData = GetAsOfData(
                new PackageDownloads("Newtonsoft.Json", "9.0.1", 123),
                new PackageDownloads("Knapcode.TorSharp", "1.0.1", 456),
                new PackageDownloads("Knapcode.TorSharp", "1.0.2", 789));

            // Act
            await Target.WriteAsync(versionSet, asOfData, Writer);

            // Assert
            AssertLines(
                new[]
                {
                    "0001-01-01T00:00:00Z,newtonsoft.json,newtonsoft.json/9.0.1,Newtonsoft.Json,9.0.1,123,123",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.1,Knapcode.TorSharp,1.0.1,456,1245",
                    "0001-01-01T00:00:00Z,knapcode.torsharp,knapcode.torsharp/1.0.2,Knapcode.TorSharp,1.0.2,789,1245",
                });
        }

        public DownloadsToCsvUpdaterTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
            Writer = new StringWriter();
        }

        public IAuxiliaryFileUpdater<AsOfData<PackageDownloads>> Target => Host.Services.GetRequiredService<IAuxiliaryFileUpdater<AsOfData<PackageDownloads>>>();
        public StringWriter Writer { get; }

        private void AssertLines(IEnumerable<string> expectedLines)
        {
            var expected = expectedLines.ToList();

            var actual = Writer
                .ToString()
                .Trim('\r', '\n')
                .Split('\n')
                .Select(x => x.Trim('\r', '\n'))
                .ToList();
            Assert.Equal("AsOfTimestamp,LowerId,Identity,Id,Version,Downloads,TotalDownloads", actual[0]);
            actual.RemoveAt(0);

            for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
            {
                Assert.Equal(expected[i], actual[i]);
            }

            Assert.Equal(expected.Count, actual.Count);
        }

        public AsOfData<PackageDownloads> GetAsOfData(params PackageDownloads[] entries)
        {
            return GetAsOfData(entries.AsEnumerable());
        }

        public AsOfData<PackageDownloads> GetAsOfData(IEnumerable<PackageDownloads> entries)
        {
            return new AsOfData<PackageDownloads>(
                DateTimeOffset.MinValue,
                "https://example.com/v3/index.json",
                "foo",
                entries.ToAsyncEnumerable());
        }

        public VersionSet GetVersionSet(params (string Id, string Version, bool IsDeleted)[] packages)
        {
            return GetVersionSet(packages.AsEnumerable());
        }

        public VersionSet GetVersionSet(IEnumerable<(string Id, string Version, bool IsDeleted)> packages)
        {
            var idToVersionToDeleted = new CaseInsensitiveDictionary<ReadableKey<CaseInsensitiveDictionary<ReadableKey<bool>>>>();
            foreach (var package in packages)
            {
                if (!idToVersionToDeleted.TryGetValue(package.Id, out var pair))
                {
                    pair = ReadableKey.Create(package.Id, new CaseInsensitiveDictionary<ReadableKey<bool>>());
                    idToVersionToDeleted.Add(package.Id, pair);
                }

                pair.Value[package.Version] = ReadableKey.Create(package.Version, package.IsDeleted); 
            }

            return new VersionSet(DateTimeOffset.MinValue, idToVersionToDeleted);
        }
    }
}
