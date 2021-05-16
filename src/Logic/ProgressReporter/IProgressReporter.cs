using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface IProgressReporter
    {
        Task ReportProgressAsync(decimal percent, string message);
    }
}
