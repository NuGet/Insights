// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class VersionSet : IVersionSet
    {
        private readonly CaseInsensitiveDictionary<ReadableKey<CaseInsensitiveDictionary<ReadableKey<bool>>>> _idToVersionToDeleted;
        private readonly HashSet<string> _uncheckedIds;
        private readonly CaseInsensitiveDictionary<HashSet<string>> _idToUncheckedVersions;

        public VersionSet(DateTimeOffset commitTimestamp, CaseInsensitiveDictionary<ReadableKey<CaseInsensitiveDictionary<ReadableKey<bool>>>> idToVersionToDeleted)
        {
            CommitTimestamp = commitTimestamp;
            _idToVersionToDeleted = idToVersionToDeleted;

            _uncheckedIds = new HashSet<string>(_idToVersionToDeleted.Keys, StringComparer.OrdinalIgnoreCase);
            _idToUncheckedVersions = new CaseInsensitiveDictionary<HashSet<string>>();
            foreach (var pair in _idToVersionToDeleted)
            {
                _idToUncheckedVersions.Add(pair.Key, new HashSet<string>(pair.Value.Value.Keys, StringComparer.OrdinalIgnoreCase));
            }
        }

        public DateTimeOffset CommitTimestamp { get; }

        public IReadOnlyCollection<string> GetUncheckedIds()
        {
            return _uncheckedIds;
        }

        public IReadOnlyCollection<string> GetUncheckedVersions(string id)
        {
            if (_idToUncheckedVersions.TryGetValue(id, out var versions))
            {
                return versions;
            }

            return Array.Empty<string>();
        }

        public bool TryGetId(string id, out string outId)
        {
            if (_idToVersionToDeleted.TryGetValue(id, out var pair))
            {
                outId = pair.Key;
                _uncheckedIds.Remove(id);
                return true;
            }

            outId = null;
            return false;
        }

        public bool TryGetVersion(string id, string version, out string outVersion)
        {
            if (!_idToVersionToDeleted.TryGetValue(id, out var versionToDeleted))
            {
                outVersion = null;
                return false;
            }

            if (versionToDeleted.Value.TryGetValue(version, out var pair))
            {
                outVersion = pair.Key;
                _idToUncheckedVersions[id].Remove(version);
                return true;
            }

            outVersion = null;
            return false;
        }
    }
}
