// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class CrossCheckConsistencyReport : IConsistencyReport
    {
        public CrossCheckConsistencyReport(bool isConsistent, bool doPackageContentsMatch)
        {
            IsConsistent = isConsistent;
            DoPackageContentsMatch = doPackageContentsMatch;
        }

        public bool IsConsistent { get; }
        public bool DoPackageContentsMatch { get; }
    }
}
