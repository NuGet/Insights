// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights
{
    public interface ITelemetryClient
    {
        IMetric GetMetric(string metricId);
        IMetric GetMetric(string metricId, string dimension1Name);
        IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name);
        IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name);
        void TrackMetric(string name, double value, IDictionary<string, string> properties);
    }
}
