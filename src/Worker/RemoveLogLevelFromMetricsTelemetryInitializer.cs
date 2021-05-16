using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGet.Insights.Worker
{
    public class RemoveLogLevelFromMetricsTelemetryInitializer : ITelemetryInitializer
    {
        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry is MetricTelemetry metric)
            {
                metric.Properties.Remove("LogLevel");
            }
        }
    }
}
