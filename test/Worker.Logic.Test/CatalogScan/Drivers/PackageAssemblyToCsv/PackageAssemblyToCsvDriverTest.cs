using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Worker.PackageAssemblyToCsv
{
    public class PackageAssemblyToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageAssemblyToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageAssembly> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageAssembly>>();

        [Fact]
        public async Task HandlesInvalidPublicKey()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.10.11.03.47.42/sharepointpnpcoreonline.2.21.1712.json",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2018-10-11T03:47:42.1892419Z"),
                PackageId = "SharePointPnPCoreOnline",
                PackageVersion = "2.21.1712",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageAssemblyResultType.ValidAssembly, record.ResultType);
            Assert.True(record.PublicKeyTokenHasSecurityException);
            Assert.True(record.HasPublicKey);
            Assert.Null(record.PublicKeyToken);
            Assert.Equal("Gt7fjUhg9y4YDKVONm/Pm3ykcVw=", record.PublicKeySHA1);
            Assert.Equal(288, record.PublicKeyLength);
        }

        [Fact]
        public async Task HandlesInvalidCultureWhenReadingAssemblyName()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.18.08.44.52/enyutrynuget.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2018-12-18T08:44:52.1808182Z"),
                PackageId = "EnyuTryNuget",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Records);
            Assert.Equal(PackageAssemblyResultType.ValidAssembly, record.ResultType);
            Assert.True(record.AssemblyNameHasCultureNotFoundException);
            Assert.Equal("EnyuTryNuget", record.Culture);
            Assert.Null(record.AssemblyNameHasFileLoadException);
        }

        [Fact]
        public async Task HandlesFileLoadExceptionWhenReadingAssemblyName()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.11.04.41.19/getaddress.azuretablestorage.1.0.0.json",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2018-12-11T04:41:19.7918122Z"),
                PackageId = "getAddress.AzureTableStorage",
                PackageVersion = "1.0.0",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            Assert.Equal(2, output.Value.Records.Count);
            var record = output.Value.Records[1];
            Assert.Equal("lib/net451/getAddress,Azure.4.5.1.dll", record.Path);
            Assert.Equal(PackageAssemblyResultType.ValidAssembly, record.ResultType);
            Assert.True(record.AssemblyNameHasFileLoadException);
            Assert.Null(record.AssemblyNameHasCultureNotFoundException);
        }

        [Fact]
        public async Task HandlesInvalidZipEntry()
        {
            var leaf = new CatalogLeafItem
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.10.06.47/microsoft.dotnet.interop.1.0.0-prerelease-0002.json",
                Type = CatalogLeafType.PackageDetails,
                CommitTimestamp = DateTimeOffset.Parse("2018-12-14T10:06:47.962422Z"),
                PackageId = "Microsoft.DotNet.Interop",
                PackageVersion = "1.0.0-prerelease-0002",
            };
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

            Assert.Equal(DriverResultType.Success, output.Type);
            Assert.All(output.Value.Records, x => Assert.Equal(PackageAssemblyResultType.InvalidZipEntry, x.ResultType));
        }

        [Fact]
        public async Task HandlesMaxWriterConcurrency()
        {
            // Arrange
            TempStreamDirectory tempDir = null;
            ConfigureSettings = x =>
            {
                x.MaxTempMemoryStreamSize = 0;
                tempDir = x.TempDirectories[0];
                tempDir.Path = Path.GetFullPath(tempDir.Path);
                tempDir.MaxConcurrentWriters = 1;
            };
            await Host.Services.GetRequiredService<StorageLeaseService>().InitializeAsync();
            await Target.InitializeAsync();

            using (var serviceScope = Host.Services.CreateScope())
            {
                var leaseScopeA = serviceScope.ServiceProvider.GetRequiredService<TempStreamLeaseScope>();
                await using var ownershipA = leaseScopeA.TakeOwnership();
                Assert.True(await leaseScopeA.WaitAsync(tempDir));

                var leaseScopeB = Host.Services.GetRequiredService<TempStreamLeaseScope>();
                await using var ownershipB = leaseScopeB.TakeOwnership();
                var leaf = new CatalogLeafItem
                {
                    Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.10.06.47/microsoft.dotnet.interop.1.0.0-prerelease-0002.json",
                    Type = CatalogLeafType.PackageDetails,
                    CommitTimestamp = DateTimeOffset.Parse("2018-12-14T10:06:47.962422Z"),
                    PackageId = "Microsoft.DotNet.Interop",
                    PackageVersion = "1.0.0-prerelease-0002",
                };

                // Act
                var output = await Target.ProcessLeafAsync(leaf, attemptCount: 1);

                // Assert
                Assert.Equal(DriverResultType.TryAgainLater, output.Type);
            }
        }
    }
}
