using System.Threading.Tasks;
using Knapcode.ExplorePackages.Worker.PackageArchiveEntryToCsv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.CatalogScan.Drivers.PackageArchiveEntryToCsv
{
    public class PackageArchiveEntryToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageArchiveEntryToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            ThrowOnLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageArchiveEntry> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageArchiveEntry>>();

        [Fact]
        public async Task ReturnsDeleted()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2017.11.08.17.42.28/nuget.platform.1.0.0.json",
                Type = CatalogLeafType.PackageDelete,
                PackageId = "NuGet.Platform",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value);
            Assert.Equal(PackageArchiveEntryResultType.Deleted, record.ResultType);
        }

        [Fact]
        public async Task ReturnsEmptyIfMissing()
        {
            // This package was deleted by a subsequent leaf.
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2015.06.13.03.41.09/nuget.platform.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "NuGet.Platform",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            Assert.Empty(output.Value);
        }

        [Fact]
        public async Task GetsPackageArchiveEntries()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.08.28.22.26.57/loshar.my.package.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Loshar.My.Package",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            Assert.Equal(6, output.Value.Count);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[0].ResultType);
            Assert.Equal(0, output.Value[0].SequenceNumber);
            Assert.Equal("_rels/.rels", output.Value[0].Path);
            Assert.Equal(".rels", output.Value[0].FileName);
            Assert.Equal(".rels", output.Value[0].FileExtension);
            Assert.Equal("_rels", output.Value[0].TopLevelFolder);
            Assert.Equal(511, output.Value[0].UncompressedSize);
            Assert.Equal(3811040653, output.Value[0].Crc32);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[1].ResultType);
            Assert.Equal(1, output.Value[1].SequenceNumber);
            Assert.Equal("Loshar.My.Package.nuspec", output.Value[1].Path);
            Assert.Equal("Loshar.My.Package.nuspec", output.Value[1].FileName);
            Assert.Equal(".nuspec", output.Value[1].FileExtension);
            Assert.Null(output.Value[1].TopLevelFolder);
            Assert.Equal(734, output.Value[1].UncompressedSize);
            Assert.Equal(3125932907, output.Value[1].Crc32);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[2].ResultType);
            Assert.Equal(2, output.Value[2].SequenceNumber);
            Assert.Equal("lib/netstandard2.0/NuGet.Services.EndToEnd.TestPackage.dll", output.Value[2].Path);
            Assert.Equal("NuGet.Services.EndToEnd.TestPackage.dll", output.Value[2].FileName);
            Assert.Equal(".dll", output.Value[2].FileExtension);
            Assert.Equal("lib", output.Value[2].TopLevelFolder);
            Assert.Equal(4608, output.Value[2].UncompressedSize);
            Assert.Equal(2664195616, output.Value[2].Crc32);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[3].ResultType);
            Assert.Equal(3, output.Value[3].SequenceNumber);
            Assert.Equal("[Content_Types].xml", output.Value[3].Path);
            Assert.Equal("[Content_Types].xml", output.Value[3].FileName);
            Assert.Equal(".xml", output.Value[3].FileExtension);
            Assert.Null(output.Value[3].TopLevelFolder);
            Assert.Equal(465, output.Value[3].UncompressedSize);
            Assert.Equal(3159194846, output.Value[3].Crc32);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[4].ResultType);
            Assert.Equal(4, output.Value[4].SequenceNumber);
            Assert.Equal("package/services/metadata/core-properties/47c2175eaf1d4949936eff3bca7bd113.psmdcp", output.Value[4].Path);
            Assert.Equal("47c2175eaf1d4949936eff3bca7bd113.psmdcp", output.Value[4].FileName);
            Assert.Equal(".psmdcp", output.Value[4].FileExtension);
            Assert.Equal("package", output.Value[4].TopLevelFolder);
            Assert.Equal(695, output.Value[4].UncompressedSize);
            Assert.Equal(3933553402, output.Value[4].Crc32);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[5].ResultType);
            Assert.Equal(5, output.Value[5].SequenceNumber);
            Assert.Equal(".signature.p7s", output.Value[5].Path);
            Assert.Equal(".signature.p7s", output.Value[5].FileName);
            Assert.Equal(".p7s", output.Value[5].FileExtension);
            Assert.Null(output.Value[5].TopLevelFolder);
            Assert.Equal(18683, output.Value[5].UncompressedSize);
            Assert.Equal(2180012886, output.Value[5].Crc32);
        }

        [Fact]
        public async Task AcceptsDuplicateEntries()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2019.12.03.16.44.55/microsoft.extensions.configuration.3.1.0.json",
                Type = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.Extensions.Configuration",
                PackageVersion = "3.1.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            Assert.Equal(11, output.Value.Count);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[6].ResultType);
            Assert.Equal(6, output.Value[6].SequenceNumber);
            Assert.Equal("packageIcon.png", output.Value[6].Path);
            Assert.Equal("packageIcon.png", output.Value[6].FileName);
            Assert.Equal(".png", output.Value[6].FileExtension);
            Assert.Null(output.Value[6].TopLevelFolder);
            Assert.Equal(7006, output.Value[6].UncompressedSize);
            Assert.Equal(3714772846, output.Value[6].Crc32);

            Assert.Equal(PackageArchiveEntryResultType.AvailableEntries, output.Value[7].ResultType);
            Assert.Equal(7, output.Value[7].SequenceNumber);
            Assert.Equal("packageIcon.png", output.Value[7].Path);
            Assert.Equal("packageIcon.png", output.Value[7].FileName);
            Assert.Equal(".png", output.Value[7].FileExtension);
            Assert.Null(output.Value[7].TopLevelFolder);
            Assert.Equal(7006, output.Value[7].UncompressedSize);
            Assert.Equal(3714772846, output.Value[7].Crc32);
        }
    }
}
