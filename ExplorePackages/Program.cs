using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Commands;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Support;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args, CancellationToken token)
        {
            // Initialize the dependency injection container.
            var serviceCollection = InitializeServiceCollection();
            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                // Determine the commands to run.
                var log = serviceProvider.GetRequiredService<ILogger>();
                if (args.Length == 0)
                {
                    log.LogError("You must provide a parameter.");
                    return;
                }

                var commands = new List<ICommand>();
                switch (args[0].Trim().ToLowerInvariant())
                {
                    case "nuspecqueries":
                        commands.Add(serviceProvider.GetRequiredService<PackageQueriesCommand>());
                        break;
                    case "fetchcursors":
                        commands.Add(serviceProvider.GetRequiredService<FetchCursorsCommand>());
                        break;
                    case "catalogtodatabase":
                        commands.Add(serviceProvider.GetRequiredService<CatalogToDatabaseCommand>());
                        break;
                    case "catalogtonuspecs":
                        commands.Add(serviceProvider.GetRequiredService<CatalogToNuspecsCommand>());
                        break;
                    case "showqueryresults":
                        commands.Add(serviceProvider.GetRequiredService<ShowQueryResultsCommand>());
                        break;
                    case "showrepositories":
                        commands.Add(serviceProvider.GetRequiredService<ShowRepositoriesCommand>());
                        break;
                    case "update":
                        commands.Add(serviceProvider.GetRequiredService<FetchCursorsCommand>());
                        commands.Add(serviceProvider.GetRequiredService<CatalogToDatabaseCommand>());
                        commands.Add(serviceProvider.GetRequiredService<CatalogToNuspecsCommand>());
                        commands.Add(serviceProvider.GetRequiredService<PackageQueriesCommand>());
                        break;
                    default:
                        log.LogError("Unknown command.");
                        return;
                }

                // Execute.
                await InitializeGlobalState();
                foreach (var command in commands)
                {
                    await command.ExecuteAsync(args, token);
                }
            }
        }

        private static async Task InitializeGlobalState()
        {
            using (var entityContext = new EntityContext())
            {
                await entityContext.Database.EnsureCreatedAsync();
            }

            // Allow up to 32 parallel HTTP connections.
            ServicePointManager.DefaultConnectionLimit = 32;

            // Set the user agent.
            var userAgentStringBuilder = new UserAgentStringBuilder("Knapcode.ExplorePackages");
            UserAgent.SetUserAgentString(userAgentStringBuilder);
        }

        private static ServiceCollection InitializeServiceCollection()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ServiceIndexCache>();
            serviceCollection.AddSingleton<ILogger, ConsoleLogger>();
            serviceCollection.AddSingleton(
                x => new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                });
            serviceCollection.AddSingleton(
                x => new HttpSource(
                    new PackageSource("https://api.nuget.org/v3/index.json"),
                    () =>
                    {
                        var httpClientHandler = x.GetRequiredService<HttpClientHandler>();
                        var httpMessageHandler = new PassThroughHandler
                        {
                            InnerHandler = httpClientHandler
                        };
                        return Task.FromResult<HttpHandlerResource>(new HttpHandlerResourceV3(
                            httpClientHandler,
                            httpMessageHandler));
                    },
                    NullThrottle.Instance));
            serviceCollection.AddTransient(
                x => new PackagePathProvider(@"E:\data\nuget.org\packages"));
            serviceCollection.AddTransient<NuspecDownloader>();
            serviceCollection.AddTransient<RemoteCursorReader>();
            serviceCollection.AddTransient<CatalogToDatabaseProcessor>();
            serviceCollection.AddTransient<CatalogToNuspecsProcessor>();
            serviceCollection.AddTransient<RegistrationService>();

            serviceCollection.AddTransient<PackageQueriesCommand>();
            serviceCollection.AddTransient<FetchCursorsCommand>();
            serviceCollection.AddTransient<CatalogToDatabaseCommand>();
            serviceCollection.AddTransient<CatalogToNuspecsCommand>();
            serviceCollection.AddTransient<ShowQueryResultsCommand>();
            serviceCollection.AddTransient<ShowRepositoriesCommand>();

            serviceCollection.AddTransient<FindIdsEndingInDotNumberNuspecQuery>();
            serviceCollection.AddTransient<FindEmptyDependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<FindRepositoriesNuspecQuery>();
            serviceCollection.AddTransient<FindInvalidDependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<FindMissingDependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<FindMissingDependencyIdsNuspecQuery>();
            serviceCollection.AddTransient<FindPackageTypesNuspecQuery>();
            serviceCollection.AddTransient<FindSemVer2PackageVersionsNuspecQuery>();
            serviceCollection.AddTransient<FindSemVer2DependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<FindFloatingDependencyVersionsNuspecQuery>();

            serviceCollection.AddTransient<IPackageQuery, HasRegistrationDiscrepancyInOriginalHivePackageQuery>();
            serviceCollection.AddTransient<IPackageQuery, HasRegistrationDiscrepancyInGzippedHivePackageQuery>();
            serviceCollection.AddTransient<IPackageQuery, HasRegistrationDiscrepancyInSemVer2HivePackageQuery>();

            // Add all of the .nuspec queries as package queries.
            var nuspecQueryDescriptors = serviceCollection
                .Where(x => typeof(INuspecQuery).IsAssignableFrom(x.ServiceType))
                .ToList();
            foreach (var nuspecQueryDescriptor in nuspecQueryDescriptors)
            {
                serviceCollection.AddTransient<IPackageQuery>(x =>
                {
                    var nuspecQuery = (INuspecQuery) x.GetRequiredService(nuspecQueryDescriptor.ImplementationType);
                    return new NuspecPackageQuery(nuspecQuery);
                });
            }

            return serviceCollection;
        }
    }
}
