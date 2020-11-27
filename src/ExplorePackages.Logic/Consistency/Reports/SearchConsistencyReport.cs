using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchConsistencyReport : IConsistencyReport
    {
        public SearchConsistencyReport(
            bool isConsistent,
            IReadOnlyDictionary<string, bool> baseUrlHasPackageSemVer1,
            IReadOnlyDictionary<string, bool> baseUrlHasPackageSemVer2,
            IReadOnlyDictionary<string, bool> baseUrlIsListedSemVer1,
            IReadOnlyDictionary<string, bool> baseUrlIsListedSemVer2)
        {
            IsConsistent = isConsistent;
            BaseUrlHasPackageSemVer1 = baseUrlHasPackageSemVer1;
            BaseUrlHasPackageSemVer2 = baseUrlHasPackageSemVer2;
            BaseUrlIsListedSemVer1 = baseUrlIsListedSemVer1;
            BaseUrlIsListedSemVer2 = baseUrlIsListedSemVer2;
        }

        public bool IsConsistent { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer1 { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlHasPackageSemVer2 { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlIsListedSemVer1 { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlIsListedSemVer2 { get; }
    }
}
