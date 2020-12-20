using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Knapcode.ExplorePackages.Worker
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
