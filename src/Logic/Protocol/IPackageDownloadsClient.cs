using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface IPackageDownloadsClient
    {
        Task<PackageDownloadSet> GetPackageDownloadSetAsync(string etag);
    }
}