using Microsoft.Extensions.Logging;

namespace NuGet.Insights.Tool
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
