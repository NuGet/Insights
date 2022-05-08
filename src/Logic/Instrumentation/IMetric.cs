// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public interface IMetric
    {
        void TrackValue(double metricValue);
        bool TrackValue(double metricValue, string dimension1Value);
        bool TrackValue(double metricValue, string dimension1Value, string dimension2Value);
        bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value);
        bool TrackValue(double metricValue, string dimension1Value, string dimension2Value, string dimension3Value, string dimension4Value);
    }
}
