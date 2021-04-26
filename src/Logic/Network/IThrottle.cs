using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface IThrottle : NuGet.Protocol.IThrottle, MiniZip.IThrottle
    {
        new Task WaitAsync();
        new void Release();
    }
}
