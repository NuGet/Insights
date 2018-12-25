using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class LeaseService : ILeaseService
    {
        private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(15);

        private readonly EntityContextFactory _entityContextFactory;

        public LeaseService(
            EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public async Task<LeaseResult> GetAsync(string name)
        {
            using (var entityContext = _entityContextFactory.Get())
            {
                return await GetAsync(name, entityContext);
            }
        }
        
        private async Task<LeaseResult> GetAsync(string name, IEntityContext entityContext)
        {
            var lease = await entityContext
                .Leases
                .Where(x => x.Name == name)
                .FirstOrDefaultAsync();

            return new LeaseResult(lease, acquired: false);
        }

        public async Task<LeaseResult> TryAcquireAsync(string name)
        {
            using (var entityContext = _entityContextFactory.Get())
            {
                var lease = await entityContext
                    .Leases
                    .Where(x => x.Name == name)
                    .FirstOrDefaultAsync();

                if (lease == null)
                {
                    entityContext.Leases.Add(new LeaseEntity
                    {
                        Name = name,
                        End = DateTimeOffset.UtcNow.Add(LeaseDuration),
                    });

                    try
                    {
                        await entityContext.SaveChangesAsync();
                        return new LeaseResult(lease, acquired: true);
                    }
                    catch (Exception ex) when (entityContext.IsUniqueConstraintViolationException(ex))
                    {
                        return await GetAsync(name, entityContext);
                    }
                }
                else
                {
                    if (lease.End.HasValue && lease.End.Value > DateTimeOffset.UtcNow)
                    {
                        return new LeaseResult(lease, acquired: false);
                    }

                    lease.End = DateTimeOffset.UtcNow.Add(LeaseDuration);

                    try
                    {
                        await entityContext.SaveChangesAsync();
                        return new LeaseResult(lease, acquired: true);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        return await GetAsync(name, entityContext);
                    }
                }
            }
        }
    }
}
