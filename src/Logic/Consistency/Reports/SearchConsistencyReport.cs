// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
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
