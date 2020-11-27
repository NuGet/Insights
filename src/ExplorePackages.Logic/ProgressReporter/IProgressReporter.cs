using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface IProgressReporter
    {
        Task ReportProgressAsync(decimal percent, string message);
    }
}
