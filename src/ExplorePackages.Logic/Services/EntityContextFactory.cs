using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;

namespace Knapcode.ExplorePackages.Logic
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
