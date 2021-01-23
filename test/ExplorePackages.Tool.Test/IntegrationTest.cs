using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Knapcode.ExplorePackages.Tool
{
    public class IntegrationTest : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private readonly TestDirectory _testDirectory;
        private readonly string _databaseConnectionString;
        private readonly string _storageConnectionString;
        private readonly string _storageContainerName;

        public IntegrationTest(ITestOutputHelper output)
        {
            _output = output;
            _testDirectory = TestDirectory.Create();
            _databaseConnectionString = "Data Source=" + Path.Combine(_testDirectory, "Knapcode.ExplorePackages.sqlite3");
            _storageConnectionString = "UseDevelopmentStorage=true";
            _storageContainerName = Guid.NewGuid().ToString();

            _output.WriteLine($"Using directory: {_testDirectory}");
            _output.WriteLine($"Using container: {_storageContainerName}");
            _output.WriteLine(string.Empty);
        }

        public async Task InitializeAsync()
        {
            var container = GetContainer();
            await container.CreateAsync();
            await container.SetPermissionsAsync(new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Blob,
            });
        }

        public async Task DisposeAsync()
        {
            _testDirectory.Dispose();
            await GetContainer().DeleteIfExistsAsync();
        }

        private CloudBlobContainer GetContainer()
        {
            var account = CloudStorageAccount.Parse(_storageConnectionString);
            var blobClient = account.CreateCloudBlobClient();
            var container = blobClient.GetContainerReference(_storageContainerName);
            return container;
        }

        [Fact]
        public async Task Run()
        {
            // Arrange
            var defaultCursor = DateTimeOffset.UtcNow.AddMinutes(-20);
            var serviceCollection = Program.InitializeServiceCollection();

            serviceCollection.AddTransient(x => new CursorService(
                x.GetRequiredService<EntityContextFactory>(),
                defaultCursor));

            serviceCollection.AddLogging(o =>
            {
                o.SetMinimumLevel(LogLevel.Trace);
                o.AddProvider(new XunitLoggerProvider(_output));
            });

            serviceCollection.Configure<ExplorePackagesEntitiesSettings>(x =>
            {
                x.DatabaseConnectionString = _databaseConnectionString;
                x.DownloadsV1Path = Path.Combine(_testDirectory, "downloads.txt");
                x.RunBoringQueries = true;
                x.WorkerCount = 8;
                Configure(x);
            });

            serviceCollection.Configure<ExplorePackagesSettings>(Configure);

            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                // Act
                var exitCode = await Program.ExecuteAsync(
                    new[] { "update" },
                    serviceProvider,
                    CancellationToken.None);

                // Assert
                using (var entityContext = await serviceProvider.GetRequiredService<EntityContextFactory>().GetAsync())
                {
                    Assert.NotEqual(0, await entityContext.CatalogCommits.CountAsync());
                    Assert.NotEqual(0, await entityContext.CatalogLeaves.CountAsync());
                    Assert.NotEqual(0, await entityContext.CatalogPackageRegistrations.CountAsync());
                    Assert.NotEqual(0, await entityContext.CatalogPackages.CountAsync());
                    Assert.NotEqual(0, await entityContext.CatalogPages.CountAsync());
                    // Assert.NotEqual(0, await entityContext.CommitCollectorProgressTokens.CountAsync());
                    Assert.NotEqual(0, await entityContext.Cursors.CountAsync());
                    // Assert.NotEqual(0, await entityContext.ETags.CountAsync());
                    Assert.NotEqual(0, await entityContext.Frameworks.CountAsync());
                    Assert.NotEqual(0, await entityContext.Leases.CountAsync());
                    Assert.NotEqual(0, await entityContext.PackageArchives.CountAsync());
                    Assert.NotEqual(0, await entityContext.PackageDependencies.CountAsync());
                    // Assert.NotEqual(0, await entityContext.PackageDownloads.CountAsync());
                    Assert.NotEqual(0, await entityContext.PackageEntries.CountAsync());
                    Assert.NotEqual(0, await entityContext.PackageQueries.CountAsync());
                    Assert.NotEqual(0, await entityContext.PackageQueryMatches.CountAsync());
                    Assert.NotEqual(0, await entityContext.PackageRegistrations.CountAsync());
                    Assert.NotEqual(0, await entityContext.Packages.CountAsync());
                    Assert.NotEqual(0, await entityContext.V2Packages.CountAsync());
                }

                Assert.Equal(0, exitCode);
            }
        }

        private void Configure(ExplorePackagesSettings x)
        {
            x.StorageConnectionString = _storageConnectionString;
            x.DownloadsV1Url = null;
            x.StorageContainerName = _storageContainerName;
            x.IsStorageContainerPublic = true;
        }
    }
}
