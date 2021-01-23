using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using McMaster.Extensions.CommandLineUtils;

namespace Knapcode.ExplorePackages.Tool
{
    public class ShowRepositoriesCommand : ICommand
    {
        private readonly PackageQueryService _queryService;
        private readonly NuspecStore _nuspecStore;

        public ShowRepositoriesCommand(
            PackageQueryService queryService,
            NuspecStore nuspecStore)
        {
            _queryService = queryService;
            _nuspecStore = nuspecStore;
        }

        public void Configure(CommandLineApplication app)
        {
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            Console.Write("Package Key");
            Console.Write('\t');
            Console.Write("ID");
            Console.Write('\t');
            Console.Write("Version");
            Console.Write('\t');
            Console.Write("Has Type");
            Console.Write('\t');
            Console.Write("Type");
            Console.Write('\t');
            Console.Write("Has URL");
            Console.Write('\t');
            Console.Write("URL");
            Console.Write('\t');
            Console.Write("Is Valid Absolute URI");
            Console.Write('\t');
            Console.Write("Scheme");
            Console.Write('\t');
            Console.Write("Authority");
            Console.Write('\t');
            Console.Write("First Commit Timestamp");
            Console.Write('\t');
            Console.Write("Last Commit Timestamp");
            Console.WriteLine();

            int count;
            long lastKey = 0;
            do
            {
                var matches = await _queryService.GetMatchedPackagesAsync(
                    PackageQueryNames.FindRepositoriesNuspecQuery,
                    lastKey);
                count = matches.Packages.Count;
                lastKey = matches.LastKey;

                foreach (var match in matches.Packages)
                {
                    var nuspecContext = await _nuspecStore.GetNuspecContextAsync(match.PackageRegistration.Id, match.Version);
                    var repositoryEl = NuspecUtility.GetRepository(nuspecContext.Document);
                    var typeAttr = repositoryEl.Attribute("type");
                    var hasType = typeAttr != null;
                    var urlAttr = repositoryEl.Attribute("url");
                    var hasUrl = urlAttr != null;
                    var isValidAbsoluteUri = Uri.TryCreate(urlAttr?.Value, UriKind.Absolute, out var parsedUrl);

                    Console.Write(match.PackageKey);
                    Console.Write('\t');
                    Console.Write(match.PackageRegistration);
                    Console.Write('\t');
                    Console.Write(match.Version);
                    Console.Write('\t');
                    Console.Write(hasType);
                    Console.Write('\t');
                    Console.Write(typeAttr?.Value);
                    Console.Write('\t');
                    Console.Write(hasUrl);
                    Console.Write('\t');
                    Console.Write(urlAttr?.Value);
                    Console.Write('\t');
                    Console.Write(isValidAbsoluteUri);
                    Console.Write('\t');
                    Console.Write(parsedUrl?.Scheme);
                    Console.Write('\t');
                    Console.Write(parsedUrl?.Authority);
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.CatalogPackage.FirstCommitTimestamp));
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.CatalogPackage.LastCommitTimestamp));
                    Console.WriteLine();
                }
            }
            while (count > 0);
        }

        private string FormatDateTimeOffset(long? ticks)
        {
            if (!ticks.HasValue)
            {
                return string.Empty;
            }

            var dateTimeOffset = new DateTimeOffset(ticks.Value, TimeSpan.Zero);
            return dateTimeOffset.ToString("G", CultureInfo.InvariantCulture);
        }

        public bool IsInitializationRequired()
        {
            return true;
        }

        public bool IsDatabaseRequired()
        {
            return true;
        }

        public bool IsSingleton()
        {
            return false;
        }
    }
}
