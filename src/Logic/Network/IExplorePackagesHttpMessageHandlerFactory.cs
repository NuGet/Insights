using System.Net.Http;

namespace Knapcode.ExplorePackages
{
    public interface IExplorePackagesHttpMessageHandlerFactory
    {
        DelegatingHandler Create();
    }
}
