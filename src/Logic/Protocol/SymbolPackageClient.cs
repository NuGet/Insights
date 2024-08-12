// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class SymbolPackageClient
    {
        private readonly IOptions<NuGetInsightsSettings> _options;

        public SymbolPackageClient(IOptions<NuGetInsightsSettings> options)
        {
            _options = options;
        }

        public string GetSymbolPackageUrl(string id, string version)
        {
            return $"{_options.Value.SymbolPackagesContainerBaseUrl.TrimEnd('/')}/" +
                $"{id.ToLowerInvariant()}." +
                $"{NuGetVersion.Parse(version).ToNormalizedString().ToLowerInvariant()}.snupkg";
        }
    }
}
