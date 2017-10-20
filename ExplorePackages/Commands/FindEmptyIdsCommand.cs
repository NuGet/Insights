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
using Knapcode.ExplorePackages.Logic;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Commands
{
    public class FindEmptyIdsCommand : ICommand
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ILogger _log;

        public FindEmptyIdsCommand(PackagePathProvider pathProvider, ILogger log)
        {
            _pathProvider = pathProvider;
            _log = log;
        }

        public Task ExecuteAsync(CancellationToken token)
        {
            var paths = GeneratePathsFromDatabase();

            var complete = 0;
            var stopwatch = Stopwatch.StartNew();
            foreach (var path in paths)
            {
                if (HasEmptyIdOrVersion(path))
                {
                    _log.LogInformation("Empty ID: " + path);
                }

                complete++;
                if (complete % 10000 == 0)
                {
                    _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");
                }
            }

            return Task.CompletedTask;
        }

        private IEnumerable<string> GeneratePathsFromDatabase()
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
                        var nuspecPath = _pathProvider.GetLatestNuspecPath(package.Id, package.Version);
                        if (File.Exists(nuspecPath))
                        {
                            yield return nuspecPath;
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
        
        private bool HasEmptyIdOrVersion(string path)
        {
            try
            {
                XDocument doc;
                using (var stream = File.OpenRead(path))
                {
                    doc = LoadXml(stream);
                }
                
                var metadataEl = doc
                    .Root
                    .Elements()
                    .Where(x => x.Name.LocalName == "metadata")
                    .FirstOrDefault();

                if (metadataEl == null)
                {
                    throw new InvalidDataException("No <metadata> element was found!");
                }

                var ns = metadataEl.GetDefaultNamespace();

                var dependenciesEl = metadataEl.Element(ns.GetName("dependencies"));
                if (dependenciesEl == null)
                {
                    return false;
                }

                var dependenyName = ns.GetName("dependency");
                var dependencyEls = dependenciesEl
                    .Elements(ns.GetName("group"))
                    .SelectMany(x => x.Elements(dependenyName))
                    .Concat(dependenciesEl.Elements(dependenyName));

                foreach (var dependencyEl in dependencyEls)
                {
                    var id = dependencyEl.Attribute("id")?.Value;
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                _log.LogError("Failed: " + path + Environment.NewLine + "  " + e.Message);
            }

            return false;
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
    }
}
