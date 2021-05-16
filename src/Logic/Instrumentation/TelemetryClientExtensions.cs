using System.Runtime.CompilerServices;

namespace NuGet.Insights
{
    public static class TelemetryClientExtensions
    {
        public static QueryLoopMetrics StartQueryLoopMetrics(
            this ITelemetryClient telemetryClient,
            [CallerFilePath] string sourceFilePath = "",
            [CallerMemberName] string memberName = "")
        {
            return QueryLoopMetrics.New(telemetryClient, sourceFilePath, memberName);
        }
    }
}
