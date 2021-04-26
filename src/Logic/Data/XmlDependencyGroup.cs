using System.Collections.Generic;

namespace Knapcode.ExplorePackages
{
    public class XmlDependencyGroup
    {
        public XmlDependencyGroup(string targetFramework, IReadOnlyList<XmlDependency> dependencies)
        {
            TargetFramework = targetFramework;
            Dependencies = dependencies;
        }

        public string TargetFramework { get; }
        public IReadOnlyList<XmlDependency> Dependencies { get; }
    }
}
