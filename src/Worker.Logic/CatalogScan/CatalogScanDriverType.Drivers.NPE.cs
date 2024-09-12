// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial struct CatalogScanDriverType
    {
        /// <summary>
        /// Implemented by <see cref="NuGetPackageExplorerToCsv.NuGetPackageExplorerToCsvDriver"/>. This driver runs
        /// NuGet Package Explorer (NPE) assembly and symbol verification logic.
        /// </summary>
        public static CatalogScanDriverType NuGetPackageExplorerToCsv { get; } = new CatalogScanDriverType(nameof(NuGetPackageExplorerToCsv));
    }
}
