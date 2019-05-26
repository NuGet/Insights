using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Logic.Worker
{
    public static class MessageEnqueuerExtensions
    {
        public static async Task EnqueueAsync(this IMessageEnqueuer enqueuer, PackageQueryMessage message)
        {
            await enqueuer.EnqueueAsync(new[] { message });
        }
    }
}
