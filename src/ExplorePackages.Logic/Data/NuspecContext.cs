using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecContext
    {
        public NuspecContext(bool exists, XDocument document)
        {
            Exists = exists;
            Document = document;
        }

        public static NuspecContext FromStream(string id, string version, Stream stream, ILogger logger)
        {
            if (stream == null)
            {
                return new NuspecContext(exists: false, document: null);
            }

            try
            {
                var document = XmlUtility.LoadXml(stream);
                return new NuspecContext(exists: true, document: document);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not parse .nuspec for {Id} {Version}.", id, version);
                throw;
            }
        }

        public bool Exists { get; }
        public XDocument Document { get; }
    }
}
