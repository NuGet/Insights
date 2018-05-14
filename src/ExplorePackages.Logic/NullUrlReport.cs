using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class NullUrlReport : IUrlReport
    {
        public static readonly Lazy<NullUrlReport> _instance = new Lazy<NullUrlReport>(() => new NullUrlReport());

        public static NullUrlReport Instance => _instance.Value;

        public Task ReportRequestAsync(Guid id, HttpRequestMessage request)
        {
            return Task.CompletedTask;
        }

        public Task ReportResponseAsync(Guid id, HttpResponseMessage response, TimeSpan duration)
        {
            return Task.CompletedTask;
        }
    }
}
