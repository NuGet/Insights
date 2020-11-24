using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic.Worker;
using Knapcode.ExplorePackages.Logic.Worker.FindPackageAssets;
using Knapcode.ExplorePackages.Logic.Worker.RunRealRestore;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages.Logic
{
    public static class ServiceCollectionExtensions
    {
        public const string HttpClientName = "Knapcode.Explorepackages";
        
        public static IServiceCollection AddExplorePackagesSettings<T>(this IServiceCollection serviceCollection)
        {
            var localDirectory = Path.GetDirectoryName(typeof(T).Assembly.Location);
            return serviceCollection.AddExplorePackagesSettings(localDirectory);
        }

        public static IServiceCollection AddExplorePackagesSettings(
            this IServiceCollection serviceCollection,
            string localDirectory = null)
        {
            var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Directory.GetCurrentDirectory();
            var userProfilePath = Path.Combine(userProfile, "Knapcode.ExplorePackages.Settings.json");

            var localPath = Path.Combine(
                localDirectory ?? typeof(ServiceCollectionExtensions).Assembly.Location,
                "Knapcode.ExplorePackages.Settings.json");

            var configurationBuilder = new ConfigurationBuilder()
                .AddJsonFile(userProfilePath, optional: true, reloadOnChange: false)
                .AddJsonFile(localPath, optional: true, reloadOnChange: false);

            var configuration = configurationBuilder.Build();

            serviceCollection.Configure<ExplorePackagesSettings>(configuration.GetSection(ExplorePackagesSettings.DefaultSectionName));

            return serviceCollection;
        }

        public static IServiceCollection AddExplorePackages(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddMemoryCache();

            serviceCollection
                .AddHttpClient(HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    MaxConnectionsPerServer = 64,
                })
                .AddHttpMessageHandler<LoggingHandler>()
                .AddHttpMessageHandler<UrlReporterHandler>()
                .ConfigureHttpClient(httpClient =>
                {
                    UserAgent.SetUserAgent(httpClient);
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-version", "2017-04-17");
                });
            serviceCollection.AddTransient(x => x
                .GetRequiredService<IHttpClientFactory>()
                .CreateClient(HttpClientName));

            serviceCollection.AddDbContext<SqliteEntityContext>((x, dbContextOptionsBuilder) =>
            {
                var options = x.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>();
                dbContextOptionsBuilder
                    .UseSqlite(options.Value.DatabaseConnectionString);
            }, contextLifetime: ServiceLifetime.Transient);
            serviceCollection.Remove(serviceCollection.Single(x => x.ServiceType == typeof(SqliteEntityContext)));
            serviceCollection.AddTransient<Func<bool, SqliteEntityContext>>(x => includeCommitCondition =>
            {
                if (includeCommitCondition)
                {
                    return new SqliteEntityContext(
                        x.GetRequiredService<ICommitCondition>(),
                        x.GetRequiredService<DbContextOptions<SqliteEntityContext>>());
                }
                else
                {
                    return new SqliteEntityContext(
                        NullCommitCondition.Instance,
                        x.GetRequiredService<DbContextOptions<SqliteEntityContext>>());
                }
            });

            serviceCollection.AddDbContext<SqlServerEntityContext>((x, dbContextOptionsBuilder) =>
            {
                var options = x.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>();
                dbContextOptionsBuilder
                    .UseSqlServer(options.Value.DatabaseConnectionString);
            }, contextLifetime: ServiceLifetime.Transient);
            serviceCollection.Remove(serviceCollection.Single(x => x.ServiceType == typeof(SqlServerEntityContext)));
            serviceCollection.AddTransient<Func<bool, SqlServerEntityContext>>(x => includeCommitCondition =>
            {
                if (includeCommitCondition)
                {
                    return new SqlServerEntityContext(
                        x.GetRequiredService<ICommitCondition>(),
                        x.GetRequiredService<DbContextOptions<SqlServerEntityContext>>());
                }
                else
                {
                    return new SqlServerEntityContext(
                        NullCommitCondition.Instance,
                        x.GetRequiredService<DbContextOptions<SqlServerEntityContext>>());
                }
            });

            serviceCollection.AddTransient<Func<bool, IEntityContext>>(x => includeCommitCondition =>
            {
                var options = x.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>();
                switch (options.Value.DatabaseType)
                {
                    case DatabaseType.Sqlite:
                        return x.GetRequiredService<Func<bool, SqliteEntityContext>>()(includeCommitCondition);
                    case DatabaseType.SqlServer:
                        return x.GetRequiredService<Func<bool, SqlServerEntityContext>>()(includeCommitCondition);
                    default:
                        throw new NotImplementedException($"The database type '{options.Value.DatabaseType}' is not supported.");
                }
            });
            serviceCollection.AddTransient(x => x.GetRequiredService<Func<bool, IEntityContext>>()(true));
            serviceCollection.AddTransient<Func<IEntityContext>>(x => () => x.GetRequiredService<Func<bool, IEntityContext>>()(true));
            serviceCollection.AddTransient<EntityContextFactory>();
            serviceCollection.AddSingleton<ISingletonService>(x => new SingletonService(
                new DatabaseLeaseService(
                    NullCommitCondition.Instance,
                    new EntityContextFactory(
                        () => x.GetRequiredService<Func<bool, IEntityContext>>()(false))),
                x.GetRequiredService<ILogger<SingletonService>>()));
            serviceCollection.AddTransient<ICommitCondition, LeaseCommitCondition>();

            serviceCollection.AddSingleton<ServiceClientFactory>();

            serviceCollection.AddSingleton<UrlReporterProvider>();
            serviceCollection.AddTransient<UrlReporterHandler>();
            serviceCollection.AddTransient<LoggingHandler>();
            serviceCollection.AddTransient(
                x =>
                {
                    var options = x.GetRequiredService<IOptions<ExplorePackagesSettings>>();
                    return new HttpSource(
                        new PackageSource(options.Value.V3ServiceIndex),
                        () =>
                        {
                            var factory = x.GetRequiredService<IHttpMessageHandlerFactory>();
                            var httpMessageHandler = factory.CreateHandler(HttpClientName);

                            return Task.FromResult<HttpHandlerResource>(new HttpMessageHandlerResource(httpMessageHandler));
                        },
                        NullThrottle.Instance);
                });

            serviceCollection.AddTransient(
                x => new HttpZipProvider(x.GetRequiredService<HttpClient>())
                {
                    BufferSizeProvider = new ZipBufferSizeProvider(
                        firstBufferSize: 4096,
                        secondBufferSize: 4096,
                        exponent: 2)
                });
            serviceCollection.AddTransient<MZipFormat>();

            serviceCollection.AddTransient<NuspecStore>();
            serviceCollection.AddTransient<MZipStore>();
            serviceCollection.AddTransient<RemoteCursorService>();
            serviceCollection.AddTransient<IPortTester, PortTester>();
            serviceCollection.AddTransient<IPortDiscoverer, SimplePortDiscoverer>();
            serviceCollection.AddTransient<SearchServiceCursorReader>();
            serviceCollection.AddTransient<PackageQueryContextBuilder>();
            serviceCollection.AddTransient<IProgressReporter, NullProgressReporter>();
            serviceCollection.AddTransient<LatestV2PackageFetcher>();
            serviceCollection.AddTransient<LatestCatalogCommitFetcher>();
            serviceCollection.AddTransient<PackageBlobNameProvider>();
            serviceCollection.AddTransient<IFileStorageService, FileStorageService>();
            serviceCollection.AddTransient<IBlobStorageService, BlobStorageService>();
            serviceCollection.AddTransient<BlobStorageMigrator>();

            serviceCollection.AddTransient<IRawMessageEnqueuer, QueueStorageEnqueuer>();
            serviceCollection.AddTransient<IWorkerQueueFactory, UnencodedWorkerQueueFactory>();

            serviceCollection.AddSingleton<IBatchSizeProvider, BatchSizeProvider>();
            serviceCollection.AddTransient<PackageQueryCollector>();
            serviceCollection.AddTransient<PackageQueryProcessor>();
            serviceCollection.AddTransient<PackageQueryExecutor>();
            serviceCollection.AddTransient<CatalogToDatabaseProcessor>();
            serviceCollection.AddTransient<V2ToDatabaseProcessor>();
            serviceCollection.AddTransient<PackageDownloadsToDatabaseProcessor>();

            serviceCollection.AddTransient<NuspecsCommitProcessor>();
            serviceCollection.AddTransient<NuspecsCommitProcessor.Collector>();
            serviceCollection.AddTransient<MZipsCommitProcessor>();
            serviceCollection.AddTransient<MZipsCommitProcessor.Collector>();
            serviceCollection.AddTransient<MZipToDatabaseCommitProcessor>();
            serviceCollection.AddTransient<MZipToDatabaseCommitProcessor.Collector>();
            serviceCollection.AddTransient<DependenciesToDatabaseCommitProcessor>();
            serviceCollection.AddTransient<DependenciesToDatabaseCommitProcessor.Collector>();
            serviceCollection.AddTransient<DependencyPackagesToDatabaseCommitProcessor>();
            serviceCollection.AddTransient<DependencyPackagesToDatabaseCommitProcessor.Collector>();

            serviceCollection.AddTransient<PackageCatalogCommitEnumerator>();
            serviceCollection.AddTransient<PackageV2CommitEnumerator>();
            serviceCollection.AddTransient<ICommitEnumerator<PackageEntity>, PackageCatalogCommitEnumerator>();
            serviceCollection.AddTransient<ICommitEnumerator<PackageRegistrationEntity>, PackageRegistrationCommitEnumerator>();
            serviceCollection.AddTransient(x => new CursorService(x.GetRequiredService<EntityContextFactory>()));
            serviceCollection.AddTransient<IETagService, ETagService>();
            serviceCollection.AddTransient<PackageService>();
            serviceCollection.AddTransient<IPackageService, PackageService>();
            serviceCollection.AddTransient<PackageQueryService>();
            serviceCollection.AddTransient<CatalogService>();
            serviceCollection.AddTransient<PackageDependencyService>();
            serviceCollection.AddTransient<ProblemService>();
            serviceCollection.AddTransient<IDatabaseLeaseService, DatabaseLeaseService>();
            serviceCollection.AddTransient<CommitCollectorSequentialProgressService>();
            serviceCollection.AddTransient<CommitEnumerator>();

            serviceCollection.AddTransient<V2Parser>();
            serviceCollection.AddSingleton<ServiceIndexCache>();
            serviceCollection.AddTransient<GalleryClient>();
            serviceCollection.AddTransient<V2Client>();
            serviceCollection.AddTransient<PackagesContainerClient>();
            serviceCollection.AddTransient<FlatContainerClient>();
            serviceCollection.AddTransient<RegistrationClient>();
            serviceCollection.AddTransient<SearchClient>();
            serviceCollection.AddTransient<AutocompleteClient>();
            serviceCollection.AddTransient<IPackageDownloadsClient, PackageDownloadsClient>();
            serviceCollection.AddTransient<CatalogClient>();

            serviceCollection.AddTransient<GalleryConsistencyService>();
            serviceCollection.AddTransient<V2ConsistencyService>();
            serviceCollection.AddTransient<FlatContainerConsistencyService>();
            serviceCollection.AddTransient<PackagesContainerConsistencyService>();
            serviceCollection.AddTransient<RegistrationOriginalConsistencyService>();
            serviceCollection.AddTransient<RegistrationGzippedConsistencyService>();
            serviceCollection.AddTransient<RegistrationSemVer2ConsistencyService>();
            serviceCollection.AddTransient<SearchConsistencyService>();
            serviceCollection.AddTransient<PackageConsistencyService>();
            serviceCollection.AddTransient<CrossCheckConsistencyService>();

            serviceCollection.AddTransient<GenericMessageProcessor>();
            serviceCollection.AddTransient<SchemaSerializer>();
            serviceCollection.AddTransient<MessageEnqueuer>();

            serviceCollection.AddTransient<IMessageProcessor<BulkEnqueueMessage>, BulkEnqueueMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<CatalogIndexScanMessage>, CatalogIndexScanMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<CatalogPageScanMessage>, CatalogPageScanMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<CatalogLeafScanMessage>, CatalogLeafScanMessageProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<FindPackageAssetsCompactMessage>, FindPackageAssetsCompactProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<RunRealRestoreMessage>, RunRealRestoreProcessor>();
            serviceCollection.AddTransient<IMessageProcessor<RunRealRestoreCompactMessage>, RunRealRestoreCompactProcessor>();

            serviceCollection.AddTransient<CatalogScanStorageService>();
            serviceCollection.AddTransient<LatestPackageLeafStorageService>();
            serviceCollection.AddTransient<CursorStorageService>();

            serviceCollection.AddTransient<AppendResultStorageService>();
            serviceCollection.AddTransient<TaskStateStorageService>();

            serviceCollection.AddTransient<ProjectHelper>();

            serviceCollection.AddTransient<CatalogScanDriverFactory>();
            serviceCollection.AddTransient<DownloadLeavesCatalogScanDriver>();
            serviceCollection.AddTransient<DownloadPagesCatalogScanDriver>();
            serviceCollection.AddTransient<FindLatestLeavesCatalogScanDriver>();
            serviceCollection.AddTransient<FindPackageAssetsScanDriver>();

            serviceCollection.AddTransient<CatalogScanService>();

            serviceCollection.AddTransient(x => new PackageQueryFactory(
                () => x.GetRequiredService<IEnumerable<IPackageQuery>>(),
                x.GetRequiredService<IOptionsSnapshot<ExplorePackagesSettings>>()));

            // Add all of the .nuspec queries.
            foreach (var serviceType in GetClassesImplementing<INuspecQuery>())
            {
                serviceCollection.AddTransient(serviceType);
                serviceCollection.AddTransient<IPackageQuery>(x =>
                {
                    var nuspecQuery = (INuspecQuery)x.GetRequiredService(serviceType);
                    return new NuspecPackageQuery(nuspecQuery);
                });
            }

            // Add all of the package consistency queries.
            foreach (var serviceType in GetClassesImplementing<IPackageConsistencyQuery>())
            {
                serviceCollection.AddTransient(serviceType);
                serviceCollection.AddTransient<IPackageQuery>(x =>
                {
                    var nuspecQuery = (IPackageConsistencyQuery)x.GetRequiredService(serviceType);
                    return new PackageConsistencyPackageQuery(nuspecQuery);
                });
            }

            // Add all of the package queries.
            foreach (var serviceType in GetClassesImplementing<IPackageQuery>())
            {
                if (serviceType == typeof(NuspecPackageQuery)
                    || serviceType == typeof(PackageConsistencyPackageQuery))
                {
                    continue;
                }

                serviceCollection.AddTransient(typeof(IPackageQuery), serviceType);
            }

            return serviceCollection;
        }

        private static IEnumerable<Type> GetClassesImplementing<T>()
        {
            return typeof(ServiceCollectionExtensions)
                .Assembly
                .GetTypes()
                .Where(t => t.GetInterfaces().Contains(typeof(T)))
                .Where(t => t.IsClass && !t.IsAbstract);
        }
    }
}
