using System;

namespace Knapcode.ExplorePackages
{
    [Flags]
    public enum SemVerType
    {
        SemVer1 = 0,
        VersionHasPrereleaseDots = 1 << 0,
        VersionHasBuildMetadata = 1 << 1,
        DependencyMinHasPrereleaseDots = 1 << 2,
        DependencyMinHasBuildMetadata = 1 << 3,
        DependencyMaxHasPrereleaseDots = 1 << 4,
        DependencyMaxHasBuildMetadata = 1 << 5,
        SemVer2 = VersionHasPrereleaseDots
            | VersionHasBuildMetadata
            | DependencyMinHasPrereleaseDots
            | DependencyMinHasBuildMetadata
            | DependencyMaxHasPrereleaseDots
            | DependencyMaxHasBuildMetadata,
    }
}
