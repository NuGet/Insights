using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
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
