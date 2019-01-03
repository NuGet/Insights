using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Knapcode.MiniZip;
using Microsoft.EntityFrameworkCore;
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
        public static IServiceCollection AddExplorePackages(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddMemoryCache();

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
                new LeaseService(
                    NullCommitCondition.Instance,
                    new EntityContextFactory(
                        () => x.GetRequiredService<Func<bool, IEntityContext>>()(false))),
                x.GetRequiredService<ILogger<SingletonService>>()));
            serviceCollection.AddTransient<ICommitCondition, LeaseCommitCondition>();

            serviceCollection.AddSingleton<UrlReporterProvider>();
            serviceCollection.AddTransient<UrlReporterHandler>();
            serviceCollection.AddTransient<LoggingHandler>();
            serviceCollection.AddSingleton(
                x => new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                    MaxConnectionsPerServer = 32,
                });
            serviceCollection.AddTransient(
                x => new InitializeServicePointHandler(
                    connectionLeaseTimeout: TimeSpan.FromMinutes(1)));
            serviceCollection.AddTransient<HttpMessageHandler>(
                x =>
                {
                    var httpClientHandler = x.GetRequiredService<HttpClientHandler>();
                    var initializeServicePointerHander = x.GetRequiredService<InitializeServicePointHandler>();
                    var urlReporterHandler = x.GetRequiredService<UrlReporterHandler>();

                    initializeServicePointerHander.InnerHandler = httpClientHandler;
                    urlReporterHandler.InnerHandler = initializeServicePointerHander;

                    return urlReporterHandler;
                });
            serviceCollection.AddSingleton(
                x =>
                {
                    var httpMessageHandler = x.GetRequiredService<HttpMessageHandler>();
                    var loggingHandler = x.GetRequiredService<LoggingHandler>();
                    loggingHandler.InnerHandler = httpMessageHandler;
                    var httpClient = new HttpClient(loggingHandler);
                    UserAgent.SetUserAgent(httpClient);
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-ms-version", "2017-04-17");
                    return httpClient;
                });
            serviceCollection.AddSingleton(
                x =>
                {
                    var options = x.GetRequiredService<IOptions<ExplorePackagesSettings>>();
                    return new HttpSource(
                        new PackageSource(options.Value.V3ServiceIndex),
                        () =>
                        {
                            var httpClientHandler = x.GetRequiredService<HttpClientHandler>();
                            var httpMessageHandler = x.GetRequiredService<HttpMessageHandler>();
                            return Task.FromResult<HttpHandlerResource>(new HttpHandlerResourceV3(
                                httpClientHandler,
                                httpMessageHandler));
                        },
                        NullThrottle.Instance);
                });

            serviceCollection.AddSingleton(x => new SearchServiceUrlCache());
            serviceCollection.AddSingleton<ISearchServiceUrlCacheInvalidator>(
                x => x.GetRequiredService<SearchServiceUrlCache>());

            serviceCollection.AddTransient(
                x => new HttpZipProvider(
                    x.GetRequiredService<HttpClient>())
                {
                    FirstBufferSize = 4096,
                    SecondBufferSize = 4096,
                    BufferGrowthExponent = 2,
                });
            serviceCollection.AddTransient<MZipFormat>();

            serviceCollection.AddTransient<NuspecStore>();
            serviceCollection.AddTransient<MZipStore>();
            serviceCollection.AddTransient<RemoteCursorService>();
            serviceCollection.AddTransient<IPortTester, PortTester>();
            serviceCollection.AddTransient<IPortDiscoverer, SimplePortDiscoverer>();
            serviceCollection.AddTransient<SearchServiceUrlDiscoverer>();
            serviceCollection.AddTransient<SearchServiceCursorReader>();
            serviceCollection.AddTransient<PackageQueryContextBuilder>();
            serviceCollection.AddTransient<IProgressReporter, NullProgressReporter>();
            serviceCollection.AddTransient<LatestV2PackageFetcher>();
            serviceCollection.AddTransient<LatestCatalogCommitFetcher>();
            serviceCollection.AddTransient<PackageBlobNameProvider>();
            serviceCollection.AddTransient<IFileStorageService, FileStorageService>();
            serviceCollection.AddTransient<IBlobStorageService, BlobStorageService>();
            serviceCollection.AddTransient<BlobStorageMigrator>();

            serviceCollection.AddTransient<PackageQueryProcessor>();
            serviceCollection.AddTransient<CatalogToDatabaseProcessor>();
            serviceCollection.AddTransient<CatalogToNuspecsProcessor>();
            serviceCollection.AddTransient<V2ToDatabaseProcessor>();
            serviceCollection.AddTransient<PackageDownloadsToDatabaseProcessor>();

            serviceCollection.AddTransient<MZipCommitProcessor>();
            serviceCollection.AddTransient<MZipCommitCollector>();
            serviceCollection.AddTransient<MZipToDatabaseCommitProcessor>();
            serviceCollection.AddTransient<MZipToDatabaseCommitCollector>();
            serviceCollection.AddTransient<DependenciesToDatabaseCommitProcessor>();
            serviceCollection.AddTransient<DependenciesToDatabaseCommitCollector>();
            serviceCollection.AddTransient<DependencyPackagesToDatabaseCommitProcessor>();
            serviceCollection.AddTransient<DependencyPackagesToDatabaseCommitCollector>();

            serviceCollection.AddTransient<PackageCommitEnumerator>();
            serviceCollection.AddTransient<ICommitEnumerator<PackageEntity>, PackageCommitEnumerator>();
            serviceCollection.AddTransient<ICommitEnumerator<PackageRegistrationEntity>, PackageRegistrationCommitEnumerator>();
            serviceCollection.AddTransient<CursorService>();
            serviceCollection.AddTransient<IETagService, ETagService>();
            serviceCollection.AddTransient<PackageService>();
            serviceCollection.AddTransient<IPackageService, PackageService>();
            serviceCollection.AddTransient<PackageQueryService>();
            serviceCollection.AddTransient<CatalogService>();
            serviceCollection.AddTransient<PackageDependencyService>();
            serviceCollection.AddTransient<ProblemService>();
            serviceCollection.AddTransient<ILeaseService, LeaseService>();

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
            serviceCollection.AddTransient<SearchLoadBalancerConsistencyService>();
            serviceCollection.AddTransient<SearchSpecificInstancesConsistencyService>();
            serviceCollection.AddTransient<PackageConsistencyService>();
            serviceCollection.AddTransient<CrossCheckConsistencyService>();

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

            // Add all of the package queries.
            foreach (var serviceType in GetClassesImplementing<IPackageQuery>())
            {
                if (serviceType == typeof(NuspecPackageQuery))
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
