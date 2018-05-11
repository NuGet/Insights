using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Support
{
    public class MinimalConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new MinimalConsoleLogger(categoryName);
        }

        public void Dispose()
        {
        }
    }
}
