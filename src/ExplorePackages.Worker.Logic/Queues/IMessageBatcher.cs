using System.Collections.Generic;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Worker
{
    public interface IMessageBatcher
    {
        Task<IReadOnlyList<HomogeneousBatchMessage>> BatchOrNullAsync<T>(IReadOnlyList<T> messages, ISchemaSerializer<T> serializer);
    }
}