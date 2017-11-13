using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public class NullProgressReport : IProgressReport
    {
        private static readonly Lazy<NullProgressReport> LazyInstance
            = new Lazy<NullProgressReport>(() => new NullProgressReport());

        public static NullProgressReport Instance => LazyInstance.Value;

        public Task ReportProgressAsync(decimal percent, string message)
        {
            return Task.CompletedTask;
        }
    }
}
