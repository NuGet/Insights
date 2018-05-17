using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IUrlReporter
    {
        Task ReportRequestAsync(Guid id, HttpRequestMessage request);
        Task ReportResponseAsync(Guid id, HttpResponseMessage response, TimeSpan duration);
    }
}
