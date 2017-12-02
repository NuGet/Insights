using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IUrlReport
    {
        Task ReportUrlAsync(Uri uri);
    }
}
