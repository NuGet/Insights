// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Insights.Worker.NuGetPackageExplorerToCsv;

namespace NuGet.Insights.Worker
{
    public static partial class CatalogScanDriverMetadata
    {
        private partial record DriverMetadata
        {
            public static DriverMetadata NuGetPackageExplorerToCsv =>
                Csv<NuGetPackageExplorerRecord, NuGetPackageExplorerFile>(CatalogScanDriverType.NuGetPackageExplorerToCsv) with
                {
                    Title = "NuGet Package Explorer to CSV",
                    DownloadedPackageAssets = DownloadedPackageAssets.Nupkg | DownloadedPackageAssets.Snupkg,

                    // Internally the NPE analysis APIs read symbols from the Microsoft and NuGet.org symbol servers. This
                    // means that the results are unstable for a similar reason as LoadSymbolPackageArchive. Additionally,
                    // some analysis times out (NuGetPackageExplorerResultType.Timeout). However this driver is relatively
                    // costly and slow to run. Therefore we won't consider it for reprocessing.
                    UpdatedOutsideOfCatalog = false,
                };
        }
    }
}
