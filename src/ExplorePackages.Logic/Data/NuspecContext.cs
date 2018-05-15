using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecContext
    {
        public NuspecContext(string path, bool exists, XDocument document)
        {
            Path = path;
            Exists = exists;
            Document = document;
        }

        public string Path { get; }
        public bool Exists { get; }
        public XDocument Document { get; }
    }
}
