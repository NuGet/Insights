// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Text.RegularExpressions;

namespace NuGet.Insights.Worker
{
    public class PackageFilter
    {
        private readonly ITelemetryClient _telemetryClient;
        private readonly IOptions<NuGetInsightsWorkerSettings> _options;

        public PackageFilter(ITelemetryClient telemetryClient, IOptions<NuGetInsightsWorkerSettings> options)
        {
            _telemetryClient = telemetryClient;
            _options = options;
        }

        public IReadOnlyList<T> FilterCatalogLeafItems<T>(string scanId, IReadOnlyList<T> leafScans) where T : ICatalogLeafItem
        {
            List<T>? filtered = null;

            for (var i = 0; i < leafScans.Count; i++)
            {
                var leafScan = leafScans[i];
                if (IsLeafScanIgnored(scanId, leafScan))
                {
                    if (filtered is null)
                    {
                        filtered = new List<T>(leafScans.Count);
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

        private bool IsLeafScanIgnored(string scanId, ICatalogLeafItem leafScan)
        {
            for (var i = 0; i < _options.Value.IgnoredPackages.Count; i++)
            {
                var pattern = _options.Value.IgnoredPackages[i];
                if (leafScan.CommitTimestamp < pattern.MinTimestamp || leafScan.CommitTimestamp > pattern.MaxTimestamp)
                {
                    continue;
                }

                var isIgnored = Regex.IsMatch(
                    leafScan.PackageId,
                    pattern.IdRegex,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.Compiled);
                if (isIgnored)
                {
                    _telemetryClient.TrackMetric($"{nameof(PackageFilter)}.IgnoredPackage", 1, new Dictionary<string, string>
                    {
                        ["ScanId"] = scanId,
                        ["Id"] = leafScan.PackageId,
                        ["Version"] = leafScan.PackageVersion,
                        ["CommitTimestamp"] = leafScan.CommitTimestamp.ToZulu(),
                    });
                    return true;
                }
            }

            return false;
        }
    }
}
