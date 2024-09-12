// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Insights.Worker.PackageCertificateToCsv;

namespace NuGet.Insights.Worker
{
    public static partial class CatalogScanDriverMetadata
    {
        private partial record DriverMetadata
        {
            public static DriverMetadata PackageCertificateToCsv =>
                BatchCsv<PackageCertificateRecord, CertificateRecord>(CatalogScanDriverType.PackageCertificateToCsv) with
                {
                    // Certificate data is not stable because certificates can expire or be revoked. Also, certificate
                    // chain resolution is non-deterministic, so different intermediate certificates can be resolved over
                    // time. Despite this, the changes are not to significant over time so we won't reprocess.
                    UpdatedOutsideOfCatalog = false,
                    Dependencies = [CatalogScanDriverType.LoadPackageArchive],
                };
        }
    }
}
