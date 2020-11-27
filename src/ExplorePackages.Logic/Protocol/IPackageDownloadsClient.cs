using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface IPackageDownloadsClient
    {
        Task<PackageDownloadSet> GetPackageDownloadSetAsync(string etag);
    }
}