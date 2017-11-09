using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
{
    public class ImmutablePackage
    {
        private readonly Package _package;

        public ImmutablePackage(Package package)
        {
            _package = package;
        }

        public int Key => _package.Key;
        public string Id => _package.Id;
        public string Version => _package.Version;
        public string Identity => _package.Identity;
        public bool Deleted => _package.Deleted;
        public long FirstCommitTimestamp => _package.FirstCommitTimestamp;
        public long LastCommitTimestamp => _package.LastCommitTimestamp;
    }
}
