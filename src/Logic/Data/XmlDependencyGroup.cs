// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
