// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public partial class CatalogScanDriverMetadataTest : BaseWorkerLogicIntegrationTest
    {
        [Fact]
        public void GetTitleReturnsTitleOverride()
        {
            var actual = CatalogScanDriverMetadata.GetTitle(CatalogScanDriverType.NuGetPackageExplorerToCsv);
            Assert.Equal("NuGet Package Explorer to CSV", actual);
        }
    }
}
