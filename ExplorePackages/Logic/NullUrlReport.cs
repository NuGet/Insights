using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class NullUrlReport : IUrlReport
    {
        public static readonly Lazy<NullUrlReport> _instance = new Lazy<NullUrlReport>(() => new NullUrlReport());

        public static NullUrlReport Instance => _instance.Value;

        public Task ReportUrlAsync(Uri uri)
        {
            return Task.CompletedTask;
        }
    }
}
