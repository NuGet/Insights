using System.Collections.Generic;
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
            ServicePointManager.DefaultConnectionLimit = 32;
            var serviceCollection = InitializeServiceCollection();

            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                using (var entityContext = new EntityContext())
                {
                    await entityContext.Database.EnsureCreatedAsync();
                }

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
                        commands.Add(serviceProvider.GetRequiredService<NuspecQueriesCommand>());
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
                        commands.Add(serviceProvider.GetRequiredService<NuspecQueriesCommand>());
                        break;
                    default:
                        log.LogError("Unknown command.");
                        return;
                }

                foreach (var command in commands)
                {
                    await command.ExecuteAsync(args, token);
                }
            }
        }

        private static ServiceCollection InitializeServiceCollection()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<ILogger, ConsoleLogger>();
            serviceCollection.AddSingleton(x => new HttpClientHandler());
            serviceCollection.AddSingleton(x => new HttpSource(
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
            serviceCollection.AddTransient(x => new PackagePathProvider(@"E:\data\nuget.org\packages"));
            serviceCollection.AddTransient<NuspecDownloader>();
            serviceCollection.AddTransient<RemoteCursorReader>();
            serviceCollection.AddTransient<CatalogToDatabaseProcessor>();
            serviceCollection.AddTransient<CatalogToNuspecsProcessor>();

            serviceCollection.AddTransient<NuspecQueriesCommand>();
            serviceCollection.AddTransient<FetchCursorsCommand>();
            serviceCollection.AddTransient<CatalogToDatabaseCommand>();
            serviceCollection.AddTransient<CatalogToNuspecsCommand>();
            serviceCollection.AddTransient<ShowQueryResultsCommand>();
            serviceCollection.AddTransient<ShowRepositoriesCommand>();

            serviceCollection.AddTransient<INuspecQuery, FindIdsEndingInDotNumberNuspecQuery>();
            serviceCollection.AddTransient<INuspecQuery, FindEmptyDependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<INuspecQuery, FindRepositoriesNuspecQuery>();
            serviceCollection.AddTransient<INuspecQuery, FindInvalidDependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<INuspecQuery, FindMissingDependencyVersionsNuspecQuery>();
            serviceCollection.AddTransient<INuspecQuery, FindMissingDependencyIdsNuspecQuery>();
            serviceCollection.AddTransient<INuspecQuery, FindPackageTypesNuspecQuery>();

            return serviceCollection;
        }
    }
}
