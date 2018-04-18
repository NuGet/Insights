using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Entities
{
    public class FrameworkEntity
    {
        public long FrameworkKey { get; set; }
        public string Value { get; set; }
        public string OriginalValue { get; set; }

        public List<PackageDependencyEntity> PackageDependencies { get; set; }
    }
}
