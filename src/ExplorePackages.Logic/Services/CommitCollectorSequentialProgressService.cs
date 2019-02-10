using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class CommitCollectorSequentialProgressService
    {
        private readonly EntityContextFactory _entityContextFactory;

        public CommitCollectorSequentialProgressService(
            EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public async Task<int> GetSkipAsync(
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
                    return 0;
                }

                return entity.Skip;
            }
        }

        private static async Task<CommitCollectorSequentialProgressEntity> GetOrInitializeAsync(
            IEntityContext entityContext,
            string name)
        {
            var entity = await entityContext
                .CommitCollectorSequentialProgress
                .SingleOrDefaultAsync(x => x.Name == name);

            if (entity == null)
            {
                entity = new CommitCollectorSequentialProgressEntity
                {
                    Name = name,
                };

                await entityContext.CommitCollectorSequentialProgress.AddAsync(entity);
            }

            return entity;
        }

        public async Task SetSkipAsync(
            string name,
            DateTimeOffset firstCommitTimestamp,
            DateTimeOffset lastCommitTimestamp,
            int skip)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var entity = await GetOrInitializeAsync(entityContext, name);

                entity.FirstCommitTimestamp = firstCommitTimestamp.UtcTicks;
                entity.LastCommitTimestamp = lastCommitTimestamp.UtcTicks;
                entity.Skip = skip;

                await entityContext.SaveChangesAsync();
            }
        }
    }
}
