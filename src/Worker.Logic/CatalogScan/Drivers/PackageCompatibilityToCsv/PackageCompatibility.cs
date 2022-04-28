// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Insights.Worker.PackageCompatibilityToCsv
{
    public partial record PackageCompatibility : PackageRecord, ICsvRecord
    {
        public PackageCompatibility()
        {
        }

        public PackageCompatibility(Guid scanId, DateTimeOffset scanTimestamp, PackageDeleteCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageCompatibilityResultType.Deleted;
        }

        public PackageCompatibility(Guid scanId, DateTimeOffset scanTimestamp, PackageDetailsCatalogLeaf leaf) : base(scanId, scanTimestamp, leaf)
        {
            ResultType = PackageCompatibilityResultType.Available;
        }

        public PackageCompatibilityResultType ResultType { get; set; }
        public bool HasError { get; set; }
        public bool DoesNotRoundTrip { get; set; }
        public bool HasAny { get; set; }
        public bool HasUnsupported { get; set; }
        public bool HasAgnostic { get; set; }

        [KustoType("dynamic")]
        public string BrokenFrameworks { get; set; }

        [KustoType("dynamic")]
        public string NuspecReader { get; set; }

        [KustoType("dynamic")]
        public string NU1202 { get; set; }

        [KustoType("dynamic")]
        public string NuGetGallery { get; set; }

        [KustoType("dynamic")]
        public string NuGetGalleryEscaped { get; set; }

        [KustoType("dynamic")]
        public string NuGetGallerySupported { get; set; }

        [KustoType("dynamic")]
        public string NuGetGalleryBadges { get; set; }
    }
}
