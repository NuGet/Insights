using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public interface IMessageProcessor<T>
    {
        Task ProcessAsync(T message);
    }
}
