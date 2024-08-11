// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.NuGetPackageExplorerToCsv
{
    public class SourceUrlRepoInfo
    {
        public SourceUrlRepo Repo { get; set; }
        public int FileCount { get; set; }
        public string Example { get; set; }
    }
}
