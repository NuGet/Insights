using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecQueryProcessor
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly INuspecQuery _query;
        private readonly ILogger _log;

        public NuspecQueryProcessor(
            PackagePathProvider pathProvider,
            INuspecQuery query,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _query = query;
            _log = log;
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var cursorService = new CursorService();
            var start = await cursorService.GetAsync(_query.CursorName);
            var end = await cursorService.GetMinimumAsync(new[]
            {
                CursorNames.CatalogToDatabase,
                CursorNames.CatalogToNuspecs,
            });
            
            var complete = 0;
            var stopwatch = Stopwatch.StartNew();

            int batchCount;
            var enumerator = new PackageCommitEnumerator();

            do
            {
                var batches = await enumerator.GetPackageBatchesAsync(start, end);
                batchCount = batches.Count;

                foreach (var batch in batches)
                {
                    foreach (var package in batch.Packages)
                    {
                        var nuspec = GetNuspecAndMetadata(package);

                        var isMatch = false;
                        try
                        {
                            Console.WriteLine($"\"{package.LastCommitTimestamp}\"\t\"{package.Id}/{package.Version}\"");
                            isMatch = await _query.IsMatchAsync(nuspec);
                        }
                        catch (Exception e)
                        {
                            _log.LogError($"Could not query .nuspec for {nuspec.Id} {nuspec.Version}: {nuspec.Path}"
                                + Environment.NewLine
                                + "  "
                                + e.Message);
                        }

                        complete++;
                        if (complete % 1000 == 0)
                        {
                            _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");
                        }
                    }

                    start = batch.CommitTimestamp;
                    // await cursorService.SetAsync(_query.CursorName, batch.CommitTimestamp);
                }
            }
            while (batchCount > 0);
        }

        private NuspecAndMetadata GetNuspecAndMetadata(Package package)
        {
            var path = _pathProvider.GetLatestNuspecPath(package.Id, package.Version);
            var exists = false;
            XDocument document = null;
            try
            {
                if (File.Exists(path))
                {
                    exists = true;
                    using (var stream = File.OpenRead(path))
                    {
                        document = LoadDocument(stream);
                    }
                }
                else
                {
                    _log.LogWarning($"Could not find .nuspec for {package.Id} {package.Version}: {path}");
                }
            }
            catch (Exception e)
            {
                _log.LogError($"Could not parse .nuspec for {package.Id} {package.Version}: {path}"
                    + Environment.NewLine
                    + "  "
                    + e.Message);
            }

            return new NuspecAndMetadata(
                package.Id,
                package.Version,
                path,
                exists,
                document);
        }
        
        private static XDocument LoadDocument(Stream stream)
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            using (var streamReader = new StreamReader(stream))
            using (var xmlReader = XmlReader.Create(streamReader, settings))
            {
                return XDocument.Load(xmlReader, LoadOptions.None);
            }
        }
    }
}
