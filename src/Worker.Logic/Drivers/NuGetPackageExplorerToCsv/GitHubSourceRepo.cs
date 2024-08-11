// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public record GitHubSourceRepo : SourceUrlRepo
    {
        public override string Type => "GitHub";
        public string Owner { get; init; }
        public string Repo { get; init; }
        public string Ref { get; init; }
    }
}
