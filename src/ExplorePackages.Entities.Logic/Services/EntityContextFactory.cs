using System;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Entities
{
    public class EntityContextFactory
    {
        private readonly Func<IEntityContext> _getEntityContext;

        public EntityContextFactory(Func<IEntityContext> getEntityContext)
        {
            _getEntityContext = getEntityContext;
        }

        public Task<IEntityContext> GetAsync()
        {
            return Task.FromResult(_getEntityContext());
        }
    }
}
