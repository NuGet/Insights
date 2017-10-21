using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecAndMetadata
    {
        public NuspecAndMetadata(string id, string version, string path, bool exists, XDocument document)
        {
            Id = id;
            Version = version;
            Path = path;
            Exists = exists;
            Document = document;
        }

        public string Id { get; }
        public string Version { get; }
        public string Path { get; }
        public bool Exists { get; }
        public XDocument Document { get; }
    }
}
