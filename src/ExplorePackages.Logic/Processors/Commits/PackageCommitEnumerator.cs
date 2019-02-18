using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public delegate IQueryable<T> QueryEntities<T>(IEntityContext entities);

    public class PackageCommitEnumerator : ICommitEnumerator<PackageEntity>
    {
        private readonly EntityContextFactory _entityContextFactory;

        public PackageCommitEnumerator(
            EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public async Task<IReadOnlyList<EntityCommit<PackageEntity>>> GetCommitsAsync(
            DateTimeOffset start,
            DateTimeOffset end,
            int batchSize)
        {
            return await GetCommitsAsync(
                x => x.Packages,
                start,
                end,
                batchSize);
        }

        public async Task<IReadOnlyList<EntityCommit<PackageEntity>>> GetCommitsAsync(
            QueryEntities<PackageEntity> queryEntities,
            DateTimeOffset start,
            DateTimeOffset end,
            int batchSize)
        {
            return await EnumeratorUtility.GetCommitsAsync(
                (s, e, b) => GetRangeAsync(queryEntities, s, e, b),
                x => x.CatalogPackage.LastCommitTimestamp,
                InitializePackageCommit,
                start,
                end,
                batchSize);
        }

        private async Task<IReadOnlyList<PackageEntity>> GetRangeAsync(
            QueryEntities<PackageEntity> queryEntities,
            long start,
            long end,
            int batchSize)
        {
            using (var entities = await _entityContextFactory.GetAsync())
            {
                return await queryEntities(entities)
                    .Include(x => x.PackageRegistration)
                    .Include(x => x.CatalogPackage)
                    .Where(x => x.PackageRegistration != null) // https://github.com/aspnet/EntityFrameworkCore/issues/14324
                    .Where(x => x.CatalogPackage != null)
                    .Where(x => x.CatalogPackage.LastCommitTimestamp > start && x.CatalogPackage.LastCommitTimestamp <= end)
                    .OrderBy(x => x.CatalogPackage.LastCommitTimestamp)
                    .Take(batchSize)
                    .ToListAsync();
            }
        }

        private EntityCommit<PackageEntity> InitializePackageCommit(
            long commitTimestamp,
            IReadOnlyList<PackageEntity> packageGroup)
        {
            return new EntityCommit<PackageEntity>(
                new DateTimeOffset(commitTimestamp, TimeSpan.Zero),
                packageGroup);
        }
    }
}
