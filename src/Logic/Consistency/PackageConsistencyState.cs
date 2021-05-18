// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackageConsistencyState
    {
        public PackageConsistencyState()
        {
            Gallery = new GalleryState();
            PackagesContainer = new PackagesContainerState();
            FlatContainer = new FlatContainerState();
        }

        public GalleryState Gallery { get; set; }
        public PackagesContainerState PackagesContainer { get; set; }
        public FlatContainerState FlatContainer { get; set; }

        public class GalleryState
        {
            public GalleryPackageState PackageState { get; set; }
        }

        public class PackagesContainerState
        {
            public BlobMetadata PackageContentMetadata { get; set; }
        }

        public class FlatContainerState
        {
            public BlobMetadata PackageContentMetadata { get; set; }
        }
    }
}
