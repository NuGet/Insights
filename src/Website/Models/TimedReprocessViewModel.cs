// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Insights.Worker.TimedReprocess;

namespace NuGet.Insights.Website
{
    public class TimedReprocessViewModel
    {
        public IReadOnlyList<int> StaleBuckets { get; set; }
        public TimedReprocessDetails Details { get; set; }
        public List<TimedReprocessRunViewModel> Runs { get; set; }
    }
}
