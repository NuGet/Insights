// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Text.RegularExpressions;

namespace NuGet.Insights.Worker
{
    public class PackageFilter
    {
        private const string IgnoredPackageMetricName = $"{nameof(PackageFilter)}.IgnoredPackage";

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

        public void FilterCsvRecords<T>(string destContainer, List<T> records) where T : ICsvRecord<T>
        {
            if (typeof(T).IsAssignableTo(typeof(IPackageCommitRecord)))
            {
                FilterPackageCommitRecords(destContainer, records);
            }
        }

        private void FilterPackageCommitRecords<T>(string destContainer, List<T> records) where T : ICsvRecord<T>
        {
            bool anyIgnored = false;
            for (var i = 0; i < records.Count && !anyIgnored; i++)
            {
                var record = (IPackageCommitRecord)records[i];
                anyIgnored |= IsPackageCommitRecordIgnored(destContainer, record, emitMetric: false);
            }

            if (anyIgnored)
            {
                var filtered = records
                    .Cast<IPackageRecord>()
                    .Where(record => !IsPackageCommitRecordIgnored(destContainer, record, emitMetric: true))
                    .ToList();

                records.Clear();
                records.AddRange(filtered.Cast<T>());
            }
        }

        private bool IsPackageCommitRecordIgnored<T>(string destContainer, T record, bool emitMetric) where T : IPackageCommitRecord
        {
            for (var i = 0; i < _options.Value.IgnoredPackages.Count; i++)
            {
                if (IsPackageIdIgnored(_options.Value.IgnoredPackages[i], record.Id, record.CatalogCommitTimestamp))
                {
                    if (emitMetric)
                    {
                        _telemetryClient.TrackMetric(IgnoredPackageMetricName, 1, new Dictionary<string, string>
                        {
                            ["Type"] = record.GetType().Name,
                            ["DestContainer"] = destContainer,
                            ["Id"] = record.Id,
                            ["Version"] = record.Version,
                            ["CommitTimestamp"] = record.CatalogCommitTimestamp.ToZulu(),
                        });
                    }
                    return true;
                }
            }

            return false;
        }


        private bool IsLeafScanIgnored(string scanId, ICatalogLeafItem leafScan)
        {
            for (var i = 0; i < _options.Value.IgnoredPackages.Count; i++)
            {
                if (IsPackageIdIgnored(_options.Value.IgnoredPackages[i], leafScan.PackageId, leafScan.CommitTimestamp))
                {
                    _telemetryClient.TrackMetric(IgnoredPackageMetricName, 1, new Dictionary<string, string>
                    {
                        ["Type"] = leafScan.GetType().Name,
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

        private static bool IsPackageIdIgnored(IgnoredPackagePattern pattern, string id, DateTimeOffset commitTimestamp)
        {
            if (commitTimestamp < pattern.MinTimestamp || commitTimestamp > pattern.MaxTimestamp)
            {
                return false;
            }

            return Regex.IsMatch(
                id,
                pattern.IdRegex,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.Compiled);
        }
    }
}
