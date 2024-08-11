// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class HistoryVersionSet : IVersionSet
    {
        private readonly CaseInsensitiveDictionary<CaseInsensitiveDictionary<(DateTimeOffset Created, string Version)>> _idToVersionToCreated = new CaseInsensitiveDictionary<CaseInsensitiveDictionary<(DateTimeOffset Created, string Version)>>();
        private readonly CaseInsensitiveDictionary<(DateTimeOffset Created, string Id)> _idToCreated = new CaseInsensitiveDictionary<(DateTimeOffset Created, string Id)>();
        private readonly CaseInsensitiveDictionary<HashSet<string>> _checkedPackages = new CaseInsensitiveDictionary<HashSet<string>>();

        public DateTimeOffset CommitTimestamp { get; private set; }

        public void SetCommitTimestamp(DateTimeOffset commitTimestamp)
        {
            _checkedPackages.Clear();
            CommitTimestamp = commitTimestamp;
        }

        public void AddPackage(string id, string version, DateTimeOffset created)
        {
            if (!_idToCreated.TryGetValue(id, out var idPair))
            {
                _idToCreated.Add(id, (created, id));
                _idToVersionToCreated.Add(id, new CaseInsensitiveDictionary<(DateTimeOffset Created, string Version)>()
                {
                    { version, (created, version) },
                });
            }
            else
            {
                if (created < idPair.Created)
                {
                    _idToCreated[id] = (created, id);
                }

                var versionToCreated = _idToVersionToCreated[id];
                if (!versionToCreated.TryGetValue(version, out var versionPair))
                {
                    versionToCreated.Add(version, (created, version));
                }
                else if (created < versionPair.Created)
                {
                    versionToCreated[version] = (created, version);
                }
            }
        }

        public bool TryGetId(string id, out string outId)
        {
            if (!_checkedPackages.ContainsKey(id))
            {
                _checkedPackages.Add(id, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            if (_idToCreated.TryGetValue(id, out var idPair))
            {
                outId = idPair.Id;
                return idPair.Created <= CommitTimestamp;
            }

            outId = null;
            return false;
        }

        public bool TryGetVersion(string id, string version, out string outVersion)
        {
            if (!_checkedPackages.TryGetValue(id, out var versions))
            {
                versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _checkedPackages.Add(id, versions);
            }

            versions.Add(version);

            if (_idToVersionToCreated.TryGetValue(id, out var versionToCreated)
                && versionToCreated.TryGetValue(version, out var versionPair))
            {
                outVersion = versionPair.Version;
                return versionPair.Created <= CommitTimestamp;
            }

            outVersion = null;
            return false;
        }

        public IReadOnlyCollection<string> GetUncheckedIds()
        {
            var uncheckedIds = new List<string>();
            foreach (var (created, id) in _idToCreated.Values)
            {
                if (created <= CommitTimestamp
                    && !_checkedPackages.ContainsKey(id))
                {
                    uncheckedIds.Add(id);
                }
            }

            return uncheckedIds;
        }

        public IReadOnlyCollection<string> GetUncheckedVersions(string id)
        {
            if (!_idToVersionToCreated.TryGetValue(id, out var versions))
            {
                return Array.Empty<string>();
            }

            if (!_checkedPackages.TryGetValue(id, out var checkedVersions))
            {
                return versions.Keys;
            }

            var uncheckedVersions = new List<string>();
            foreach (var (created, version) in versions.Values)
            {
                if (created <= CommitTimestamp
                    && !checkedVersions.Contains(version))
                {
                    uncheckedVersions.Add(version);
                }
            }

            return uncheckedVersions;
        }
    }
}
