// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.PackageLicenseToCsv
{
    public partial record PackageLicense : PackageRecord, ICsvRecord
    {
        public PackageLicense()
        {
        }

        public PackageLicense(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageLicenseResultType.Deleted;
        }

        public PackageLicense(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf)
            : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageLicenseResultType.None;
        }

        public PackageLicenseResultType ResultType { get; set; }

        public string Url { get; set; }
        public string Expression { get; set; }
        public string File { get; set; }

        public string GeneratedUrl { get; set; }

        [KustoType("dynamic")]
        public string ExpressionParsed { get; set; }
        [KustoType("dynamic")]
        public string ExpressionLicenses { get; set; }
        [KustoType("dynamic")]
        public string ExpressionExceptions { get; set; }
        [KustoType("dynamic")]
        public string ExpressionNonStandardLicenses { get; set; }
        public bool? ExpressionHasDeprecatedIdentifier { get; set; }

        public long? FileSize { get; set; }
        public string FileSHA256 { get; set; }
        public string FileContent { get; set; }
    }
}
