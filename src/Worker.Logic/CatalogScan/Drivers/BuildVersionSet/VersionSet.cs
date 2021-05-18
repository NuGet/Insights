// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class VersionSet : IVersionSet
    {
        private readonly CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>> _idToVersionToDeleted;
        private readonly HashSet<string> _uncheckedIds;
        private readonly CaseInsensitiveDictionary<HashSet<string>> _idToUncheckedVersions;

        public VersionSet(DateTimeOffset commitTimestamp, CaseInsensitiveDictionary<CaseInsensitiveDictionary<bool>> idToVersionToDeleted)
        {
            CommitTimestamp = commitTimestamp;
            _idToVersionToDeleted = idToVersionToDeleted;

            _uncheckedIds = new HashSet<string>(_idToVersionToDeleted.Keys, StringComparer.OrdinalIgnoreCase);
            _idToUncheckedVersions = new CaseInsensitiveDictionary<HashSet<string>>();
            foreach (var pair in _idToVersionToDeleted)
            {
                _idToUncheckedVersions.Add(pair.Key, new HashSet<string>(pair.Value.Keys, StringComparer.OrdinalIgnoreCase));
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

        public bool DidIdEverExist(string id)
        {
            if (_idToVersionToDeleted.ContainsKey(id))
            {
                _uncheckedIds.Remove(id);
                return true;
            }

            return false;
        }

        public bool DidVersionEverExist(string id, string normalizedVersion)
        {
            if (!_idToVersionToDeleted.TryGetValue(id, out var versionToDeleted))
            {
                return false;
            }

            if (versionToDeleted.ContainsKey(normalizedVersion))
            {
                _idToUncheckedVersions[id].Remove(normalizedVersion);
                return true;
            }

            return false;
        }
    }
}
