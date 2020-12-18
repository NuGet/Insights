using Knapcode.ExplorePackages.Worker;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Knapcode.ExplorePackages.Worker
{
    public class TelemetryClientWrapper : ITelemetryClient
    {
        private readonly TelemetryClient _inner;

        public TelemetryClientWrapper(TelemetryClient inner)
        {
            _inner = inner;
        }

        public IMetric GetMetric(string metricId, string dimension1Name)
        {
            return new MetricWrapper(_inner.GetMetric(metricId, dimension1Name));
        }

        public IMetric GetMetric(string metricId, string dimension1Name, string dimension2Name)
        {
            return new MetricWrapper(_inner.GetMetric(metricId, dimension1Name, dimension2Name));
        }
    }
}
