using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class CommitCollectorSequentialProgressService
    {
        private readonly EntityContextFactory _entityContextFactory;

        public CommitCollectorSequentialProgressService(
            EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public async Task<string> GetSerializedProgressTokenAsync(
            string name,
            DateTimeOffset firstCommitTimestamp,
            DateTimeOffset lastCommitTimestamp)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var entity = await GetOrInitializeAsync(entityContext, name);

                if (entity.FirstCommitTimestamp != firstCommitTimestamp.UtcTicks
                    || entity.LastCommitTimestamp != lastCommitTimestamp.UtcTicks)
                {
                    return null;
                }

                return entity.SerializedProgressToken;
            }
        }

        public async Task SetSerializedProgressTokenAsync(
            string name,
            DateTimeOffset firstCommitTimestamp,
            DateTimeOffset lastCommitTimestamp,
            string serializedProgressToken)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var entity = await GetOrInitializeAsync(entityContext, name);

                entity.FirstCommitTimestamp = firstCommitTimestamp.UtcTicks;
                entity.LastCommitTimestamp = lastCommitTimestamp.UtcTicks;
                entity.SerializedProgressToken = serializedProgressToken;

                await entityContext.SaveChangesAsync();
            }
        }

        private static async Task<CommitCollectorProgressTokenEntity> GetOrInitializeAsync(
            IEntityContext entityContext,
            string name)
        {
            var entity = await entityContext
                .CommitCollectorProgressTokens
                .SingleOrDefaultAsync(x => x.Name == name);

            if (entity == null)
            {
                entity = new CommitCollectorProgressTokenEntity
                {
                    Name = name,
                };

                await entityContext.CommitCollectorProgressTokens.AddAsync(entity);
            }

            return entity;
        }
    }
}
