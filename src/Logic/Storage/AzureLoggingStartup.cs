using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace NuGet.Insights
{
    public class AzureLoggingStartup
    {
        public AzureLoggingStartup(
            AzureEventSourceLogForwarder forwarder,
            IOptions<NuGetInsightsSettings> options)
        {
            if (options.Value.EnableAzureLogging)
            {
                forwarder.Start();
            }
        }
    }
}
