using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class NuspecContext
    {
        public NuspecContext(bool exists, XDocument document)
        {
            Exists = exists;
            Document = document;
        }

        public bool Exists { get; }
        public XDocument Document { get; }
    }
}
