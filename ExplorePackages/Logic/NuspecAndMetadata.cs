using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecAndMetadata
    {
        public NuspecAndMetadata(string id, string version, string path, XDocument document)
        {
            Id = id;
            Version = version;
            Path = path;
            Document = document;
        }

        public string Id { get; }
        public string Version { get; }
        public string Path { get; }
        public XDocument Document { get; }
    }
}
