using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IProgressReporter
    {
        Task ReportProgressAsync(decimal percent, string message);
    }
}
