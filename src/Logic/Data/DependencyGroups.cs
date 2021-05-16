using System.Collections.Generic;

namespace NuGet.Insights
{
    public class DependencyGroups
    {
        public DependencyGroups(IReadOnlyList<Dependency> dependencies, IReadOnlyList<DependencyGroup> groups)
        {
            Dependencies = dependencies;
            Groups = groups;
        }

        public IReadOnlyList<Dependency> Dependencies { get; }
        public IReadOnlyList<DependencyGroup> Groups { get; }
    }
}
