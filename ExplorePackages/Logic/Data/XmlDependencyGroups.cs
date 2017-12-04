using System.Collections.Generic;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class XmlDependencyGroups
    {
        internal static readonly XmlDependencyGroups Empty = new XmlDependencyGroups(
            new List<XmlDependency>(),
            new List<XmlDependencyGroup>());

        public XmlDependencyGroups(IReadOnlyList<XmlDependency> dependencies, IReadOnlyList<XmlDependencyGroup> groups)
        {
            Dependencies = dependencies;
            Groups = groups;
        }

        public IReadOnlyList<XmlDependency> Dependencies { get; }
        public IReadOnlyList<XmlDependencyGroup> Groups { get; }
    }
}
