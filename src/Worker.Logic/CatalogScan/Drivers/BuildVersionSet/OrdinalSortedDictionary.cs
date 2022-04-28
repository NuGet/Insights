// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Insights.Worker.BuildVersionSet
{
    public class OrdinalSortedDictionary<TValue> : SortedDictionary<string, TValue>
    {
        public OrdinalSortedDictionary() : base(StringComparer.Ordinal)
        {
        }
    }
}
