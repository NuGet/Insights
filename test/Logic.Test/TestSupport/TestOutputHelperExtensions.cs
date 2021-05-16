using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace NuGet.Insights
{
    public static class TestOutputHelperExtensions
    {
        public static ILogger<T> GetLogger<T>(this ITestOutputHelper output)
        {
            var factory = new LoggerFactory();
            factory.AddXunit(output);
            return factory.CreateLogger<T>();
        }

        public static ITelemetryClient GetTelemetryClient(this ITestOutputHelper output)
        {
            return new LoggerTelemetryClient(output.GetLogger<LoggerTelemetryClient>());
        }
    }
}
