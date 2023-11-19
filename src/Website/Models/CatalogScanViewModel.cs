// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Insights.Worker;

namespace NuGet.Insights.Website
{
    public class CatalogScanViewModel
    {
        public CatalogScanDriverType DriverType { get; set; }
        public TimeSpan CursorAge { get; set; }
        public CursorTableEntity Cursor { get; set; }
        public IReadOnlyList<CatalogIndexScan> LatestScans { get; set; }
        public IReadOnlyList<CatalogScanDriverType> Dependencies { get; set; } 

        public DateTimeOffset DefaultMax { get; set; }

        public bool? OnlyLatestLeavesSupport { get; set; }
        public bool IsEnabled { get; set; }
    }
}
