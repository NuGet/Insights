// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
