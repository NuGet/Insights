using System;

namespace Knapcode.ExplorePackages.Entities
{
    public class EntityContextFactory
    {
        private readonly Func<IEntityContext> _getEntityContext;

        public EntityContextFactory(Func<IEntityContext> getEntityContext)
        {
            _getEntityContext = getEntityContext;
        }

        public IEntityContext Get()
        {
            return _getEntityContext();
        }
    }
}
