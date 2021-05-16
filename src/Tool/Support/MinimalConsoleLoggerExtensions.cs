using NuGet.Insights.Tool;

namespace Microsoft.Extensions.Logging
{
    public static class MinimalConsoleLoggerExtensions
    {
        public static ILoggingBuilder AddMinimalConsole(this ILoggingBuilder builder)
        {
            builder.AddProvider(new MinimalConsoleLoggerProvider());
            return builder;
        }

        public static ILoggerFactory AddMinimalConsole(this ILoggerFactory factory)
        {
            factory.AddProvider(new MinimalConsoleLoggerProvider());
            return factory;
        }
    }
}
