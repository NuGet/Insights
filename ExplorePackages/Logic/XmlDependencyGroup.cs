using System.Collections.Generic;
using System.Xml.Linq;

namespace Knapcode.ExplorePackages.Logic
{
    public class XmlDependencyGroup
    {
        public XmlDependencyGroup(string targetFramework, IReadOnlyList<XElement> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies;
        }

        public string TargetFramework { get; }
        public IReadOnlyList<XElement> Dependencies { get; }
    }
}
