using System;
using System.Threading;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages
{
    public interface IRemoteCursorClient
    {
        Task<DateTimeOffset> GetCatalogAsync(CancellationToken token = default);
        Task<DateTimeOffset> GetFlatContainerAsync(CancellationToken token = default);
        Task<DateTimeOffset> GetRegistrationAsync(CancellationToken token = default);
        Task<DateTimeOffset> GetSearchAsync();
    }
}