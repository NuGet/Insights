using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface IRawMessageEnqueuer
    {
        Task AddAsync(byte[] message);
    }
}