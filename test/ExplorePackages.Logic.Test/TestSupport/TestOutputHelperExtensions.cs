using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
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
