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
            var packages = GetPackages();

            var complete = 0;
            var stopwatch = Stopwatch.StartNew();
            foreach (var package in packages)
            {
                var nuspec = GetNuspecAndMetadata(package);

                var isMatch = false;
                try
                {
                    isMatch = await _query.IsMatchAsync(nuspec);
                }
                catch (Exception e)
                {
                    _log.LogError($"Could not query .nuspec for {nuspec.Id} {nuspec.Version}: {nuspec.Path}"
                        + Environment.NewLine
                        + "  "
                        + e.Message);
                }

                if (isMatch)
                {

                }

                complete++;
                if (complete % 1000 == 0)
                {
                    _log.LogInformation($"{complete} completed ({Math.Round(complete / stopwatch.Elapsed.TotalSeconds)} per second).");
                }
            }
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
                        document = LoadXml(stream);
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

        private IEnumerable<Package> GetPackages()
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
                        .Take(1000)
                        .ToList();

                    fetched = packages.Count;

                    foreach (var package in packages)
                    {
                        yield return package;
                        lastKey = Math.Max(package.Key, lastKey);
                    }
                }
            }
            while (fetched > 0);
        }
    }
}
