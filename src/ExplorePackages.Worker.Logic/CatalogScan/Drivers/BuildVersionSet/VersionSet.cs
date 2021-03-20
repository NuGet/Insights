using System;

namespace Knapcode.ExplorePackages.Worker.BuildVersionSet
{
    internal class VersionSet : IVersionSet
    {
        private readonly ReadDictionary _idToVersionToDeleted;

        public VersionSet(DateTimeOffset commitTimestamp, ReadDictionary idToVersionToDeleted)
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
