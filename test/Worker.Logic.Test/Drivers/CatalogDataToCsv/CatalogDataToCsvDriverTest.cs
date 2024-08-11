// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.CatalogDataToCsv
{
    public class CatalogDataToCsvDriverTest : BaseWorkerLogicIntegrationTest
    {
        public ICatalogLeafToCsvDriver<PackageDeprecationRecord, PackageVulnerabilityRecord, CatalogLeafItemRecord> Target => Host.Services.GetRequiredService<ICatalogLeafToCsvDriver<PackageDeprecationRecord, PackageVulnerabilityRecord, CatalogLeafItemRecord>>();

        public CatalogDataToCsvDriverTest(ITestOutputHelper output, DefaultWebApplicationFactory<StaticFilesStartup> factory) : base(output, factory)
        {
        }

        [Fact]
        public async Task SerializesDeprecationPropertiesInLexOrder()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2023.09.07.19.23.28/microsoft.aspnetcore.odata.8.2.1.json",
                PageUrl = "https://api.nuget.org/v3/catalog0/page19609.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "Microsoft.AspNetCore.OData",
                PackageVersion = "8.2.1",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Sets3.SelectMany(x => x.Records));
            Assert.Equal(
                "{\"alternatePackage\":{" +
                    "\"id\":\"Microsoft.AspNetCore.OData\"," +
                    "\"range\":\"[8.2.3, )\"}," +
                "\"message\":\"This version contains a regression change when you use $expand with \\u0027null\\u0027 field value.  Please use version 8.2.3 or beyond.\"," +
                "\"reasons\":[\"Other\",\"CriticalBugs\"]}",
                record.Deprecation);
        }

        [Fact]
        public async Task SerializesVulnerabilityPropertiesInLexOrder()
        {
            await Target.InitializeAsync();
            var leaf = new CatalogLeafScan
            {
                Url = "https://api.nuget.org/v3/catalog0/data/2020.07.21.05.06.09/system.security.cryptography.xml.4.4.0.json",
                PageUrl = "https://api.nuget.org/v3/catalog0/page10578.json",
                LeafType = CatalogLeafType.PackageDetails,
                PackageId = "System.Security.Cryptography.Xml",
                PackageVersion = "4.4.0",
            };

            var output = await Target.ProcessLeafAsync(leaf);

            Assert.Equal(DriverResultType.Success, output.Type);
            var record = Assert.Single(output.Value.Sets3.SelectMany(x => x.Records));
            Assert.Equal(
                "[{" +
                    "\"@id\":\"https://api.nuget.org/v3/catalog0/data/2020.07.21.05.06.09/system.security.cryptography.xml.4.4.0.json#vulnerability/GitHub/705\"," +
                    "\"advisoryUrl\":\"https://github.com/advisories/GHSA-rr3c-f55v-qhv5\"," +
                    "\"severity\":\"1\"" +
                "},{" +
                    "\"@id\":\"https://api.nuget.org/v3/catalog0/data/2020.07.21.05.06.09/system.security.cryptography.xml.4.4.0.json#vulnerability/GitHub/732\"," +
                    "\"advisoryUrl\":\"https://github.com/advisories/GHSA-35hc-x2cw-2j4v\"," +
                    "\"severity\":\"1\"" +
                "}]",
                record.Vulnerabilities);
        }
    }
}
