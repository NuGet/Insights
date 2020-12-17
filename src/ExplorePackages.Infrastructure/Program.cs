using System.Threading.Tasks;
using Pulumi;

namespace Knapcode.ExplorePackages
{
    public class Program
    {
        static async Task<int> Main()
        {
            var result = await Deployment.RunAsync<MyStack>();
            return result;
        }
    }
}
