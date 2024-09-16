// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageFileToCsv
{
    public class PackageFileToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public PackageFileToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory)
            : base(output, factory)
        {
            FailFastLogLevel = LogLevel.None;
            AssertLogLevel = LogLevel.None;
        }

        public ICatalogLeafToCsvDriver<PackageFileRecord> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageFileRecord>>();

        [Fact]
        public async Task HandlesInvalidZipEntry()
        {
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.10.06.47/microsoft.dotnet.interop.1.0.0-prerelease-0002.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.DotNet.Interop",
                PackageVersion = "1.0.0-prerelease-0002",
            }.SetDefaults();
            await Target.InitializeAsync();

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            Assert.Equal(3, output.Value.Count(x => x.FileExtension == ".dll"));
            Assert.All(output.Value.Where(x => x.FileExtension == ".dll"), x => Assert.Equal(FileRecordResultType.InvalidZipEntry, x.ResultType));
            Assert.All(output.Value.Where(x => x.FileExtension != ".dll"), x => Assert.Equal(FileRecordResultType.Available, x.ResultType));
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

            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2018.12.14.10.06.47/microsoft.dotnet.interop.1.0.0-prerelease-0002.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.DotNet.Interop",
                PackageVersion = "1.0.0-prerelease-0002",
            };

            using var otherHost = GetHost(Output);
            var leaseService = otherHost.Services.GetRequiredService<TempStreamDirectoryLeaseService>();
            await using var leaseA = await leaseService.WaitForLeaseAsync(tempDir);
            Assert.NotNull(leaseA);

            // Act
            var output = await Target.ProcessLeafAsync(leaf);

            // Assert
            Assert.Equal(DriverResultType.TryAgainLater, output.Type);
        }
    }
}
