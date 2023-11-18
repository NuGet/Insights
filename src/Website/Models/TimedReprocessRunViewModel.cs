// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Insights.Worker;
using NuGet.Insights.Worker.TimedReprocess;

namespace NuGet.Insights.Website
{
    public class TimedReprocessRunViewModel
    {
        public TimedReprocessRun Run { get; set; }
        public List<(TimedReprocessCatalogScan ReprocessScan, CatalogIndexScan IndexScan)> Scans { get; set; }
    }
}
