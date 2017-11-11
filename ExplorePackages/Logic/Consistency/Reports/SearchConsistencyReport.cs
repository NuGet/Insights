using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchConsistencyReport : IConsistencyReport
    {
        public SearchConsistencyReport(
            bool isConsistent,
            IReadOnlyDictionary<string, bool> baseUrlHasPackageSemVer1,
            IReadOnlyDictionary<string, bool> baseUrlHasPackageSemVer2)
        {
            IsConsistent = isConsistent;
            BaseUrlHasPackageSemVer1 = baseUrlHasPackageSemVer1;
            BaseUrlHasPackageSemVer2 = baseUrlHasPackageSemVer2;
        }

        public bool IsConsistent { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer1 { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer2 { get; }
    }
}
