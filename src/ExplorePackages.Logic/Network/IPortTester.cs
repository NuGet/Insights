using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface IPortTester
    {
        Task<bool> IsPortOpenAsync(string host, int port, TimeSpan connectTimeout);
    }
}
