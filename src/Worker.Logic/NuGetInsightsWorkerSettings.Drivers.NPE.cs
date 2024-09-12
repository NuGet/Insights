// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class NuGetInsightsWorkerSettings
    {
        public string NuGetPackageExplorerContainerName { get; set; } = "nugetpackageexplorer";
        public string NuGetPackageExplorerFileContainerName { get; set; } = "nugetpackageexplorerfiles";
    }
}
