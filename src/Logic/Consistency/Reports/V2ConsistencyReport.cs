// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class V2ConsistencyReport : IConsistencyReport
    {
        public V2ConsistencyReport(bool isConsistent, bool hasPackage)
        {
            IsConsistent = isConsistent;
            HasPackage = hasPackage;
        }

        public bool IsConsistent { get; }
        public bool HasPackage { get; }
    }
}
