using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public class NullThrottle : IThrottle, MiniZip.IThrottle, NuGet.Protocol.IThrottle
    {
        public static NullThrottle Instance { get; } = new NullThrottle();

        public void Release()
        {
        }

        public Task WaitAsync() => Task.CompletedTask;
    }
}
