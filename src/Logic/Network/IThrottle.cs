using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface IThrottle : Protocol.IThrottle, Knapcode.MiniZip.IThrottle
    {
        new Task WaitAsync();
        new void Release();
    }
}
