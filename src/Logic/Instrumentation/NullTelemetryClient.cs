// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Insights
{
    public class NullTelemetryClient : ITelemetryClient
    {
        public static NullTelemetryClient Instance { get; } = new NullTelemetryClient();

        public IMetric GetMetric(string metricId)
        {
            return NullMetric.Instance;
        }

        public IMetric GetMetric(string metricId, string dimension1Name)
        {
            return NullMetric.Instance;
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name)
        {
            return NullMetric.Instance;
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name, string dimension3Name)
        {
            return NullMetric.Instance;
        }

        public void TrackMetric(string name, double value, IDictionary<string, string> properties)
        {
        }
    }
}
