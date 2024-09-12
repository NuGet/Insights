// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial struct CatalogScanDriverType
    {
        /// <summary>
        /// Implemented by <see cref="PackageCertificateToCsv.PackageCertificateToCsvDriver"/>. Loads all certificate
        /// chain information from the package signature into storage and tracks the many-to-many relationship between
        /// packages and certificates.
        /// </summary>
        public static CatalogScanDriverType PackageCertificateToCsv { get; } = new CatalogScanDriverType(nameof(PackageCertificateToCsv));
    }
}
