using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class NullThrottle : IThrottle, Protocol.IThrottle, Knapcode.MiniZip.IThrottle
    {
        public static NullThrottle Instance { get; } = new NullThrottle();

        public void Release()
        {
        }

        public Task WaitAsync()
        {
            return Task.CompletedTask;
        }
    }
}
