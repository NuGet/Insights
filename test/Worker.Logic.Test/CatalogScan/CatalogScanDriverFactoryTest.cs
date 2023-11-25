// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverFactoryTest : BaseWorkerLogicIntegrationTest
    {
        [Theory]
        [MemberData(nameof(StartabledDriverTypesData))]
        public void Create_SupportsAllDriverTypes(CatalogScanDriverType type)
        {
            var target = Host.Services.GetRequiredService<ICatalogScanDriverFactory>();

            target.Create(type);
        }

        public CatalogScanDriverFactoryTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }
    }
}
