using System;
using System.IO;
using System.Xml.Linq;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecProvider
    {
        private readonly PackagePathProvider _pathProvider;
        private readonly ILogger _log;

        public NuspecProvider(PackagePathProvider pathProvider, ILogger log)
        {
            _pathProvider = pathProvider;
            _log = log;
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
            catch (Exception e)
            {
                _log.LogError($"Could not parse .nuspec for {id} {version}: {path}"
                    + Environment.NewLine
                    + "  "
                    + e.Message);

                throw;
            }

            return new NuspecQueryContext(path, exists, document);
        }
    }
}
