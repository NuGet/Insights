// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGet.Insights
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
