using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Knapcode.ExplorePackages.Entities;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecProcessorQueue
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly INuspecProcessor _processor;
        private readonly ILogger _log;

        public NuspecProcessorQueue(
            PackagePathProvider pathProvider,
            INuspecProcessor processor,
            ILogger log)
        {
            _pathProvider = pathProvider;
            _processor = processor;
            _log = log;
        }

        public async Task ProcessAsync(CancellationToken token)
        {
            var allWork = GenerateWorkFromDatabase();

            var complete = 0;
            var stopwatch = Stopwatch.StartNew();
            foreach (var work in allWork)
            {
                try
                {
                    using (var stream = File.OpenRead(work.Path))
                    {
                        work.Nuspec = LoadXml(stream);
                    }

                    var nuspec = new NuspecAndMetadata(work.Id, work.Version, work.Path, work.Nuspec);
                    await WorkAsync(nuspec);
                }
                catch (Exception e)
                {
                    _log.LogError("Failed: " + work.Path + Environment.NewLine + "  " + e.Message);
                }

                complete++;
                if (complete % 1000 == 0)
                {
                    _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");
                }
            }
        }
        
        private async Task WorkAsync(NuspecAndMetadata nuspec)
        {
            try
            {
                await _processor.ProcessAsync(nuspec);
            }
            catch (Exception e)
            {
                _log.LogError(e.ToString());
                throw;
            }
        }
        
        private static XDocument LoadXml(Stream stream)
        {
            using (var streamReader = new StreamReader(stream))
            using (var xmlReader = XmlReader.Create(streamReader, new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            }))
            {
                return XDocument.Load(xmlReader, LoadOptions.None);
            }
        }

        private IEnumerable<MutableNuspecToProcess> GenerateWorkFromDatabase()
        {
            int fetched;
            var lastKey = 0;
            do
            {
                using (var entities = new EntityContext())
                {
                    var packages = entities
                        .Packages
                        .Where(x => x.Key > lastKey)
                        .Where(x => !x.Deleted)
                        .Select(x => new { x.Key, x.Id, x.Version })
                        .Take(1000)
                        .ToList();
                    fetched = packages.Count;

                    foreach (var package in packages)
                    {
                        var path = _pathProvider.GetLatestNuspecPath(package.Id, package.Version);
                        if (File.Exists(path))
                        {
                            yield return new MutableNuspecToProcess
                            {
                                Id = package.Id,
                                Version = package.Version,
                                Path = path,
                            };
                        }
                        else
                        {
                            _log.LogWarning($"Could not find .nuspec for {package.Id} {package.Version}.");
                        }

                        lastKey = Math.Max(package.Key, lastKey);
                    }
                }
            }
            while (fetched > 0);
        }

        public class MutableNuspecToProcess
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public string Path { get; set; }
            public XDocument Nuspec { get; set; }
        }
    }
}
