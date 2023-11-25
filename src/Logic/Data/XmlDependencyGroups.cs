// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class XmlDependencyGroups
    {
        internal static readonly XmlDependencyGroups Empty = new XmlDependencyGroups(
            new List<XmlDependency>(),
            new List<XmlDependencyGroup>());

        public XmlDependencyGroups(IReadOnlyList<XmlDependency> dependencies, IReadOnlyList<XmlDependencyGroup> groups)
        {
            Dependencies = dependencies;
            Groups = groups;
        }

        public IReadOnlyList<XmlDependency> Dependencies { get; }
        public IReadOnlyList<XmlDependencyGroup> Groups { get; }
    }
}
