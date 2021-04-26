using System.Xml.Linq;

namespace Knapcode.ExplorePackages
{
    public class XmlDependency
    {
        public XmlDependency(string id, string version, XElement element)
        {
            Id = id;
            Version = version;
            Element = element;
        }

        public string Id { get; }
        public string Version { get; }
        public XElement Element { get; }
    }
}
