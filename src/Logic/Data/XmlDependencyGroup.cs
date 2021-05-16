using System.Collections.Generic;

namespace NuGet.Insights
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
