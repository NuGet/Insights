using System.Collections.Generic;
using NuGet.Frameworks;

namespace Knapcode.ExplorePackages.Logic
{
    public class DependencyGroup
    {
        public DependencyGroup(string targetFramework, NuGetFramework parsedTargetFramework, IReadOnlyList<Dependency> dependencies)
        {
            TargetFramework = targetFramework;
            ParsedTargetFramework = parsedTargetFramework;
            Dependencies = dependencies;
        }

        public string TargetFramework { get; }
        public NuGetFramework ParsedTargetFramework { get; }
        public IReadOnlyList<Dependency> Dependencies { get; }
    }
}
