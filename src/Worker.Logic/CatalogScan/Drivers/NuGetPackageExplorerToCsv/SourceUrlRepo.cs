// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.Json.Serialization;

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    [JsonPolymorphic]
    [JsonDerivedType(typeof(GitHubSourceRepo))]
    [JsonDerivedType(typeof(InvalidSourceRepo))]
    [JsonDerivedType(typeof(UnknownSourceRepo))]
    public abstract record SourceUrlRepo
    {
        public abstract string Type { get; }
    }
}
