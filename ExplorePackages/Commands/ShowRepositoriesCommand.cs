using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class ShowRepositoriesCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ILogger _log;

        public ShowRepositoriesCommand(PackagePathProvider pathProvider, ILogger log)
        {
            _pathProvider = pathProvider;
            _log = log;
        }

        public async Task ExecuteAsync(IReadOnlyList<string> args, CancellationToken token)
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

            var matchesService = new PackageQueryService(_log);

            int count;
            long lastKey = 0;
            do
            {
                var matches = await matchesService.GetMatchedPackagesAsync(
                    PackageQueryNames.FindRepositoriesNuspecQuery,
                    lastKey);
                count = matches.Packages.Count;
                lastKey = matches.LastKey;

                foreach (var match in matches.Packages)
                {
                    var path = _pathProvider.GetLatestNuspecPath(match.Id, match.Version);
                    XDocument nuspec;
                    using (var stream = File.OpenRead(path))
                    {
                        nuspec = NuspecUtility.LoadXml(stream);
                    }

                    var repositoryEl = NuspecUtility.GetRepository(nuspec);
                    var typeAttr = repositoryEl.Attribute("type");
                    var hasType = typeAttr != null;
                    var urlAttr = repositoryEl.Attribute("url");
                    var hasUrl = urlAttr != null;
                    var isValidAbsoluteUri = Uri.TryCreate(urlAttr?.Value, UriKind.Absolute, out Uri parsedUrl);

                    Console.Write(match.Key);
                    Console.Write('\t');
                    Console.Write(match.Id);
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
                    Console.Write(FormatDateTimeOffset(match.FirstCommitTimestamp));
                    Console.Write('\t');
                    Console.Write(FormatDateTimeOffset(match.LastCommitTimestamp));
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
    }
}
