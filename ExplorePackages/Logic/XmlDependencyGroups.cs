using System.Collections.Generic;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class XmlDependencyGroups
    {
        internal static readonly XmlDependencyGroups Empty = new XmlDependencyGroups(
            new List<XElement>(),
            new List<XmlDependencyGroup>());

        public XmlDependencyGroups(IReadOnlyList<XElement> dependencies, IReadOnlyList<XmlDependencyGroup> groups)
        {
            Dependencies = dependencies;
            Groups = groups;
        }

        public IReadOnlyList<XElement> Dependencies { get; }
        public IReadOnlyList<XmlDependencyGroup> Groups { get; }
    }
}
