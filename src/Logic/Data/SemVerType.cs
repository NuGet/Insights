// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
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
    }
}
