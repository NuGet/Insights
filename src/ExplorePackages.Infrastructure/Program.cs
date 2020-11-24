using System.Threading.Tasks;
using Pulumi;

namespace Knapcode.ExplorePackages
{
    public class Program
    {
        static Task<int> Main() => Deployment.RunAsync<MyStack>();
    }
}
