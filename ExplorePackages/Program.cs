using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Commands;
using Knapcode.ExplorePackages.Entities;
using Knapcode.ExplorePackages.Logic;
using Knapcode.ExplorePackages.Support;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace Knapcode.ExplorePackages
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 32;

            args = new[] { "update" };
            //args = new[] { "showqueryresults", NuspecQueryNames.FindRepositoriesNuspecQuery };
            //args = new[] { "showrepositories" };

            MainAsync(args, CancellationToken.None).Wait();
        }

        private static async Task MainAsync(string[] args, CancellationToken token)
        {
            using (var entityContext = new EntityContext())
            {
                await entityContext.Database.EnsureCreatedAsync();
            }

            var log = new ConsoleLogger();
            
            if (args.Length == 0)
            {
                log.LogError("You must provide a parameter.");
                return;
            }

            var httpSource = new HttpSource(
                new PackageSource("https://api.nuget.org/v3/index.json"),
                () =>
                {
                    var httpClientHandler = new HttpClientHandler();
                    var httpMessageHandler = new PassThroughHandler { InnerHandler = httpClientHandler };
                    return Task.FromResult<HttpHandlerResource>(new HttpHandlerResourceV3(
                        httpClientHandler,
                        httpMessageHandler));
                },
                NullThrottle.Instance);

            var packagePathProvider = new PackagePathProvider(@"E:\data\nuget.org\packages");

            var nuspecDownloader = new NuspecDownloader(
                packagePathProvider,
                httpSource,
                log);

            var commands = new List<ICommand>();

            switch (args[0].Trim().ToLowerInvariant())
            {
                case "nuspecqueries":
                    commands.Add(GetNuspecQueriesCommand(log, packagePathProvider));
                    break;
                case "fetchcursors":
                    commands.Add(GetFetchCursorsCommand(log, httpSource));
                    break;
                case "catalogtodatabase":
                    commands.Add(GetCatalogToDatabaseCommand(log));
                    break;
                case "catalogtonuspecs":
                    commands.Add(GetCatalogToNuspecsCommand(log, nuspecDownloader));
                    break;
                case "showqueryresults":
                    commands.Add(GetShowQueryResultsCommand(log));
                    break;
                case "showrepositories":
                    commands.Add(GetShowRepositoriesCommand(log, packagePathProvider));
                    break;
                case "update":
                    commands.Add(GetFetchCursorsCommand(log, httpSource));
                    commands.Add(GetCatalogToDatabaseCommand(log));
                    commands.Add(GetCatalogToNuspecsCommand(log, nuspecDownloader));
                    commands.Add(GetNuspecQueriesCommand(log, packagePathProvider));
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

        private static ShowRepositoriesCommand GetShowRepositoriesCommand(ConsoleLogger log, PackagePathProvider packagePathProvider)
        {
            return new ShowRepositoriesCommand(
                packagePathProvider,
                log);
        }

        private static ShowQueryResultsCommand GetShowQueryResultsCommand(ConsoleLogger log)
        {
            return new ShowQueryResultsCommand(
                log);
        }

        private static CatalogToNuspecsCommand GetCatalogToNuspecsCommand(ConsoleLogger log, NuspecDownloader nuspecDownloader)
        {
            return new CatalogToNuspecsCommand(
                new CatalogToNuspecsProcessor(
                    nuspecDownloader,
                    log),
                log);
        }

        private static CatalogToDatabaseCommand GetCatalogToDatabaseCommand(ConsoleLogger log)
        {
            return new CatalogToDatabaseCommand(
                new CatalogToDatabaseProcessor(
                    log),
                log);
        }

        private static FetchCursorsCommand GetFetchCursorsCommand(ConsoleLogger log, HttpSource httpSource)
        {
            return new FetchCursorsCommand(
                new RemoteCursorReader(
                    httpSource,
                    log),
                log);
        }

        private static NuspecQueriesCommand GetNuspecQueriesCommand(ConsoleLogger log, PackagePathProvider packagePathProvider)
        {
            return new NuspecQueriesCommand(
                packagePathProvider,
                new FindMissingDependencyIdsNuspecQuery(log),
                new FindRepositoriesNuspecQuery(log),
                new FindPackageTypesNuspecQuery(log),
                new FindInvalidDependencyVersionsNuspecQuery(log),
                new FindMissingDependencyVersionsNuspecQuery(log),
                new FindEmptyDependencyVersionsNuspecQuery(log),
                log);
        }
    }
}
