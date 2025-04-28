// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Text.RegularExpressions;

namespace NuGet.Insights.Worker
{
    public class PackageFilter
    {
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageFilter(IOptions<NuGetInsightsWorkerSettings> options)
        {
            _options = options;
        }

        public IReadOnlyList<CatalogLeafScan> FilterCatalogLeafScans(IReadOnlyList<CatalogLeafScan> leafScans)
        {
            List<CatalogLeafScan>? filtered = null;

            for (var i = 0; i < leafScans.Count; i++)
            {
                var leafScan = leafScans[i];
                if (!IsLeafScanIncluded(leafScan))
                {
                    if (filtered is null)
                    {
                        filtered = new List<CatalogLeafScan>(leafScans.Count);
                        for (var j = 0; j < i; j++)
                        {
                            filtered.Add(leafScans[j]);
                        }
                    }
                }
                else if (filtered is not null)
                {
                    filtered.Add(leafScan);
                }
            }

            return filtered ?? leafScans;
        }

        private bool IsLeafScanIncluded(CatalogLeafScan leafScan)
        {
            return IsPackageIdIncluded(leafScan.PackageId, leafScan.CommitTimestamp);
        }

        private bool IsPackageIdIncluded(string id, DateTimeOffset commitTimestamp)
        {
            for (var i = 0; i < _options.Value.IgnoredPackages.Count; i++)
            {
                var pattern = _options.Value.IgnoredPackages[i];
                if (commitTimestamp < pattern.MinTimestamp || commitTimestamp > pattern.MaxTimestamp)
                {
                    continue;
                }

                var isIgnored = Regex.IsMatch(
                    id,
                    pattern.IdRegex,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.Compiled);
                if (isIgnored)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
