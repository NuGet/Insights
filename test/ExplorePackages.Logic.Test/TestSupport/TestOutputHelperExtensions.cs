using Microsoft.Extensions.Logging;
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
    }
}
