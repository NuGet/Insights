using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPortTester
    {
        Task<bool> IsPortOpenAsync(string host, int port, TimeSpan connectTimeout);
    }
}
