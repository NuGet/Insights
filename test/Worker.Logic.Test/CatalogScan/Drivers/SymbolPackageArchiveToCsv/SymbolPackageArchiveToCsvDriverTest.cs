// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.Worker.SymbolPackageArchiveToCsv
{
    public class SymbolPackageArchiveToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public SymbolPackageArchiveToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<SymbolPackageArchiveRecord, SymbolPackageArchiveEntry>>();

        [Fact]
        public async Task ReturnsDeleted()
        {
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2017.11.08.17.42.28/nuget.platform.1.0.0.json",
                LeafType = CatalogLeafType.PackageDelete,
                CommitTimestamp = DateTimeOffset.Parse("2017-11-08T17:42:28.5677911Z", CultureInfo.InvariantCulture),
                PackageId = "NuGet.Platform",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(ArchiveResultType.Deleted, record.ResultType);
            var entry = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(ArchiveResultType.Deleted, entry.ResultType);
        }

        [Fact]
        public async Task ReturnsDoesNotExistIfMissing()
        {
            // This package was deleted by a subsequent leaf.
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2015.06.13.03.41.09/nuget.platform.1.0.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2015-06-13T03:41:09.5185838Z", CultureInfo.InvariantCulture),
                PackageId = "NuGet.Platform",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(ArchiveResultType.DoesNotExist, record.ResultType);
            var entry = Assert.Single(Assert.Single(output.Value.Sets2).Records);
            Assert.Equal(ArchiveResultType.DoesNotExist, entry.ResultType);
            Assert.Contains("Metric emitted: FileDownloader.GetZipDirectoryReaderAsync.NotFound NuGet.Platform 1.0.0 Snupkg DefaultMiniZip = 1", LogMessages);
        }

        [Fact]
        public async Task GetsPackageArchiveEntries()
        {
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2021.08.06.00.31.41/knapcode.torsharp.2.6.0.json",
                LeafType = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("021-08-06T00:31:41.2929519Z", CultureInfo.InvariantCulture),
                PackageId = "Knapcode.TorSharp",
                PackageVersion = "2.6.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var archive = Assert.Single(Assert.Single(output.Value.Sets1).Records);
            Assert.Equal(ArchiveResultType.Available, archive.ResultType);
            Assert.Equal(35657, archive.Size);
            Assert.Equal("OB0k0WV5orupQ25KxGMknQ==", archive.HeaderMD5);
            Assert.Equal("al45MNQtsMepvs8zfPii/9IUuOUc8oVn8PpT7xxmZtl/x6UabhiS4MW5UdywZaUryLQ5m7T94KG3RIRhJXq28g==", archive.HeaderSHA512);
            Assert.Equal(35639, archive.OffsetAfterEndOfCentralDirectory);
            Assert.Equal(482u, archive.CentralDirectorySize);
            Assert.Equal(35153u, archive.OffsetOfCentralDirectory);
            Assert.Equal(6, archive.EntryCount);
            Assert.Empty(archive.Comment);

            Assert.Equal(6, Assert.Single(output.Value.Sets2).Records.Count);
            var entries = Assert.Single(output.Value.Sets2).Records;

            Assert.Equal(0, entries[0].SequenceNumber);
            Assert.Equal("_rels/.rels", entries[0].Path);
            Assert.Equal(".rels", entries[0].FileName);
            Assert.Equal(".rels", entries[0].FileExtension);
            Assert.Equal("_rels", entries[0].TopLevelFolder);
            Assert.Equal(0, entries[0].Flags);
            Assert.Equal(8, entries[0].CompressionMethod);
            Assert.Equal(DateTimeOffset.Parse("2021-03-06T16:28:30.0000000+00:00", CultureInfo.InvariantCulture), entries[0].LastModified);
            Assert.Equal(4198631635, entries[0].Crc32);
            Assert.Equal(286u, entries[0].CompressedSize);
            Assert.Equal(511u, entries[0].UncompressedSize);
            Assert.Equal(0u, entries[0].LocalHeaderOffset);
            Assert.Empty(entries[0].Comment);

            Assert.Equal(1, entries[1].SequenceNumber);
            Assert.Equal("Knapcode.TorSharp.nuspec", entries[1].Path);
            Assert.Equal("Knapcode.TorSharp.nuspec", entries[1].FileName);
            Assert.Equal(".nuspec", entries[1].FileExtension);
            Assert.Null(entries[1].TopLevelFolder);
            Assert.Equal(0, entries[1].Flags);
            Assert.Equal(8, entries[1].CompressionMethod);
            Assert.Equal(DateTimeOffset.Parse("2021-03-06T16:28:30.0000000+00:00", CultureInfo.InvariantCulture), entries[1].LastModified);
            Assert.Equal(3823684902, entries[1].Crc32);
            Assert.Equal(1728u, entries[1].CompressedSize);
            Assert.Equal(3777u, entries[1].UncompressedSize);
            Assert.Equal(327u, entries[1].LocalHeaderOffset);
            Assert.Empty(entries[1].Comment);

            Assert.Equal(2, entries[2].SequenceNumber);
            Assert.Equal("lib/net45/Knapcode.TorSharp.pdb", entries[2].Path);
            Assert.Equal("Knapcode.TorSharp.pdb", entries[2].FileName);
            Assert.Equal(".pdb", entries[2].FileExtension);
            Assert.Equal("lib", entries[2].TopLevelFolder);
            Assert.Equal(0, entries[2].Flags);
            Assert.Equal(8, entries[2].CompressionMethod);
            Assert.Equal(DateTimeOffset.Parse("2021-03-07T00:28:30.0000000+00:00", CultureInfo.InvariantCulture), entries[2].LastModified);
            Assert.Equal(3116723293, entries[2].Crc32);
            Assert.Equal(14284u, entries[2].CompressedSize);
            Assert.Equal(27192u, entries[2].UncompressedSize);
            Assert.Equal(2109u, entries[2].LocalHeaderOffset);
            Assert.Empty(entries[2].Comment);

            Assert.Equal(3, entries[3].SequenceNumber);
            Assert.Equal("lib/netstandard2.0/Knapcode.TorSharp.pdb", entries[3].Path);
            Assert.Equal("Knapcode.TorSharp.pdb", entries[3].FileName);
            Assert.Equal(".pdb", entries[3].FileExtension);
            Assert.Equal("lib", entries[3].TopLevelFolder);
            Assert.Equal(0, entries[3].Flags);
            Assert.Equal(8, entries[3].CompressionMethod);
            Assert.Equal(DateTimeOffset.Parse("2021-03-07T00:28:30.0000000+00:00", CultureInfo.InvariantCulture), entries[3].LastModified);
            Assert.Equal(3552305110, entries[3].Crc32);
            Assert.Equal(17806u, entries[3].CompressedSize);
            Assert.Equal(34544u, entries[3].UncompressedSize);
            Assert.Equal(16454u, entries[3].LocalHeaderOffset);
            Assert.Empty(entries[3].Comment);

            Assert.Equal(4, entries[4].SequenceNumber);
            Assert.Equal("[Content_Types].xml", entries[4].Path);
            Assert.Equal("[Content_Types].xml", entries[4].FileName);
            Assert.Equal(".xml", entries[4].FileExtension);
            Assert.Null(entries[4].TopLevelFolder);
            Assert.Equal(0, entries[4].Flags);
            Assert.Equal(8, entries[4].CompressionMethod);
            Assert.Equal(DateTimeOffset.Parse("2021-03-06T16:28:30.0000000+00:00", CultureInfo.InvariantCulture), entries[4].LastModified);
            Assert.Equal(1981492428u, entries[4].Crc32);
            Assert.Equal(212u, entries[4].CompressedSize);
            Assert.Equal(465u, entries[4].UncompressedSize);
            Assert.Equal(34330u, entries[4].LocalHeaderOffset);
            Assert.Empty(entries[4].Comment);

            Assert.Equal(5, entries[5].SequenceNumber);
            Assert.Equal("package/services/metadata/core-properties/add523d6d7c24a66bcf78466aedc12dc.psmdcp", entries[5].Path);
            Assert.Equal("add523d6d7c24a66bcf78466aedc12dc.psmdcp", entries[5].FileName);
            Assert.Equal(".psmdcp", entries[5].FileExtension);
            Assert.Equal("package", entries[5].TopLevelFolder);
            Assert.Equal(0, entries[5].Flags);
            Assert.Equal(8, entries[5].CompressionMethod);
            Assert.Equal(DateTimeOffset.Parse("2021-03-06T16:28:30.0000000+00:00", CultureInfo.InvariantCulture), entries[5].LastModified);
            Assert.Equal(268762850u, entries[5].Crc32);
            Assert.Equal(451u, entries[5].CompressedSize);
            Assert.Equal(730u, entries[5].UncompressedSize);
            Assert.Equal(34591u, entries[5].LocalHeaderOffset);
            Assert.Empty(entries[5].Comment);
        }
    }
}
