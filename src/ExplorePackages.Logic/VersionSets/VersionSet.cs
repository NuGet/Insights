using System;

namespace Knapcode.ExplorePackages.VersionSets
{
    public class VersionSet : IVersionSet
    {
        private readonly VersionSetDictionary _idToVersionToDeleted;

        public VersionSet(DateTimeOffset commitTimestamp, VersionSetDictionary idToVersionToDeleted)
        {
            CommitTimestamp = commitTimestamp;
            _idToVersionToDeleted = idToVersionToDeleted;
        }

        public DateTimeOffset CommitTimestamp { get; }

        public bool IsPackageAvaiable(string id, string normalizedVersion)
        {
            if (!_idToVersionToDeleted.TryGetValue(id, out var versionToDeleted))
            {
                return false;
            }

            if (!versionToDeleted.TryGetValue(normalizedVersion, out var deleted))
            {
                return false;
            }

            return !deleted;
        }
    }
}
