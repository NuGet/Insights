using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    public interface IPortDiscoverer
    {
        Task<IReadOnlyList<int>> FindPortsAsync(string host, int startingPort, bool requireSsl, TimeSpan connectTimeout);
    }
}