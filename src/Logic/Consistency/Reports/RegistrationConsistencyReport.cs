// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RegistrationConsistencyReport : IConsistencyReport
    {
        public RegistrationConsistencyReport(bool isConsistent, bool isInIndex, bool hasLeaf, bool isListedInIndex, bool isListedInLeaf)
        {
            IsConsistent = isConsistent;
            IsInIndex = isInIndex;
            HasLeaf = hasLeaf;
            IsListedInIndex = isListedInIndex;
            IsListedInLeaf = isListedInLeaf;
        }

        public bool IsConsistent { get; }
        public bool IsInIndex { get; }
        public bool HasLeaf { get; }
        public bool IsListedInIndex { get; }
        public bool IsListedInLeaf { get; }
    }
}
