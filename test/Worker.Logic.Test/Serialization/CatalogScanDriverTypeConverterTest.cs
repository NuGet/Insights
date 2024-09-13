// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Configuration;

namespace NuGet.Insights.Worker
{
    public class CatalogScanDriverTypeConverterTest
    {
        [Fact]
        public void CanConvertUsingConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    { "NuGetInsights:DisabledDrivers:0", "LoadPackageArchive" },
                    { "NuGetInsights:DisabledDrivers:1", "PackageAssetToCsv" },
                    { "NuGetInsights:DisabledDrivers:2", "PackageIconToCsv" },
                })
                .Build();
            var settings = new NuGetInsightsWorkerSettings();

            configuration.GetSection(NuGetInsightsSettings.DefaultSectionName).Bind(settings);

            Assert.Equal(3, settings.DisabledDrivers.Count);
            Assert.Equal(CatalogScanDriverType.LoadPackageArchive, settings.DisabledDrivers[0]);
            Assert.Equal(CatalogScanDriverType.PackageAssetToCsv, settings.DisabledDrivers[1]);
            Assert.Equal(CatalogScanDriverType.PackageIconToCsv, settings.DisabledDrivers[2]);
        }
    }
}
