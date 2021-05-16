using System.Net.Http;

namespace NuGet.Insights
{
    public interface INuGetInsightsHttpMessageHandlerFactory
    {
        DelegatingHandler Create();
    }
}
