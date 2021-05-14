using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

namespace Knapcode.ExplorePackages
{
    public class AzureLoggingStartup
    {
        public AzureLoggingStartup(
            AzureEventSourceLogForwarder forwarder,
            IOptions<ExplorePackagesSettings> options)
        {
            if (options.Value.EnableAzureLogging)
            {
                forwarder.Start();
            }
        }
    }
}
