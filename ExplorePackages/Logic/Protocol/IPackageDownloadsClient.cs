using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic
{
    public interface IPackageDownloadsClient
    {
        Task<PackageDownloadSet> GetPackageDownloadSetAsync(string etag);
    }
}