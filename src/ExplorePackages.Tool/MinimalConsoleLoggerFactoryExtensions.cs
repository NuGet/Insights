using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Tool
{
    public static class MinimalConsoleLoggerFactoryExtensions
    {
        public static ILoggerFactory AddMinimalConsole(this ILoggerFactory factory)
        {
            factory.AddProvider(new MinimalConsoleLoggerProvider());
            return factory;
        }
    }
}
