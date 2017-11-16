using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    public interface IPortTester
    {
        Task<bool> IsPortOpenAsync(string host, int port, bool requireSsl, TimeSpan connectTimeout);
    }
}
