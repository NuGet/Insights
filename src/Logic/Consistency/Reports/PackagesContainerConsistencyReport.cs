// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class PackagesContainerConsistencyReport : IConsistencyReport
    {
        public PackagesContainerConsistencyReport(
            bool isConsistent,
            BlobMetadata packageContentMetadata)
        {
            IsConsistent = isConsistent;
            PackageContentMetadata = packageContentMetadata;
        }

        public bool IsConsistent { get; }
        public BlobMetadata PackageContentMetadata { get; }
    }
}
