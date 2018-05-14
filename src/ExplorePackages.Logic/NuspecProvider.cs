using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecProvider
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ILogger<NuspecProvider> _logger;

        public NuspecProvider(PackagePathProvider pathProvider, ILogger<NuspecProvider> logger)
        {
            _pathProvider = pathProvider;
            _logger = logger;
        }

        public NuspecQueryContext GetNuspec(string id, string version)
        {
            var path = _pathProvider.GetLatestNuspecPath(id, version);
            var exists = false;
            XDocument document = null;
            try
            {
                if (File.Exists(path))
                {
                    exists = true;
                    using (var stream = File.OpenRead(path))
                    {
                        document = XmlUtility.LoadXml(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not parse .nuspec for {Id} {Version}: {Path}", id, version, path);
                throw;
            }

            return new NuspecQueryContext(path, exists, document);
        }
    }
}
