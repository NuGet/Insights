// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class NullMetric : IMetric
    {
        public static NullMetric Instance { get; } = new NullMetric();

        public void TrackValue(double metricValue)
        {
        }

        public bool TrackValue(double metricValue, string dimension1Value)
        {
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value)
        {
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value)
        {
            return true;
        }

        public bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value, string dimension4Value)
        {
            return true;
        }
    }
}
