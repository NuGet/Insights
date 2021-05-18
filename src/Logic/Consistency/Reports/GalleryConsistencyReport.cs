// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class GalleryConsistencyReport : IConsistencyReport
    {
        public GalleryConsistencyReport(bool isConsistent, GalleryPackageState packageState)
        {
            IsConsistent = isConsistent;
            PackageState = packageState;
        }

        public bool IsConsistent { get; }
        public GalleryPackageState PackageState { get; }
    }
}
