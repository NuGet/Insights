using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IProgressReport
    {
        Task ReportProgressAsync(decimal percent, string message);
    }
}
