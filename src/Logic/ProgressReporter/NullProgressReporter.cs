using System;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class NullProgressReporter : IProgressReporter
    {
        private static readonly Lazy<NullProgressReporter> LazyInstance
            = new Lazy<NullProgressReporter>(() => new NullProgressReporter());

        public static NullProgressReporter Instance => LazyInstance.Value;

        public Task ReportProgressAsync(decimal percent, string message)
        {
            return Task.CompletedTask;
        }
    }
}
