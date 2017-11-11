using System.Collections.Generic;

namespace Knapcode.ExplorePackages.Logic
{
    public class SearchConsistencyReport : IConsistencyReport
    {
        public SearchConsistencyReport(bool isConsistent, IReadOnlyDictionary<string, bool> baseUrlHasPackage)
        {
            IsConsistent = isConsistent;
            BaseUrlHasPackage = baseUrlHasPackage;
        }

        public bool IsConsistent { get; }
        public IReadOnlyDictionary<string, bool> BaseUrlHasPackage { get; }
    }
}
