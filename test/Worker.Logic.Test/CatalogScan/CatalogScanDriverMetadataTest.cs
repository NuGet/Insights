// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverMetadataTest : BaseWorkerLogicIntegrationTest
    {
        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetOnlyLatestLeavesSupport_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetOnlyLatestLeavesSupport(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetBucketRangeSupport_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetBucketRangeSupport(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetUpdatedOutsideOfCatalog_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetUpdatedOutsideOfCatalog(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetDefaultMin_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetDefaultMin(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetTransitiveClosure_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetTransitiveClosure(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetDependents_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetDependents(type);
        }

        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void GetDependencies_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            CatalogScanDriverMetadata.GetDependencies(type);
        }

        public CatalogScanDriverMetadataTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
