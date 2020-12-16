using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages.Entities
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExplorePackagesEntities(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddDbContext<SqliteEntityContext>((x, dbContextOptionsBuilder) =>
            {
                var options = x.GetRequiredService<IOptions<ExplorePackagesEntitiesSettings>>();
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
                var options = x.GetRequiredService<IOptions<ExplorePackagesEntitiesSettings>>();
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
                var options = x.GetRequiredService<IOptions<ExplorePackagesEntitiesSettings>>();
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

            serviceCollection.AddTransient<RemoteCursorService>();
            serviceCollection.AddTransient<PackageQueryContextBuilder>();
            serviceCollection.AddTransient<BlobStorageMigrator>();

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

            serviceCollection.AddTransient(x => new PackageQueryFactory(
                () => x.GetRequiredService<IEnumerable<IPackageQuery>>(),
                x.GetRequiredService<IOptions<ExplorePackagesEntitiesSettings>>()));

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
