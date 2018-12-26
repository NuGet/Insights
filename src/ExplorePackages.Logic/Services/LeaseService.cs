using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class LeaseService : ILeaseService
    {
        private readonly EntityContextFactory _entityContextFactory;

        public LeaseService(
            EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public async Task<LeaseEntity> GetOrNullAsync(string name)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                return await entityContext
                    .Leases
                    .Where(x => x.Name == name)
                    .FirstOrDefaultAsync();
            }
        }

        public async Task<LeaseResult> TryReleaseAsync(LeaseEntity lease)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                entityContext.Leases.Attach(lease);
                lease.End = null;

                try
                {
                    await entityContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // This means someone else has since acquired the lease. Ignore this failure.
                }

                return LeaseResult.NotLeased();
            }
        }

        public async Task<LeaseResult> RenewAsync(LeaseEntity lease, TimeSpan leaseDuration)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                entityContext.Leases.Attach(lease);
                lease.End = DateTimeOffset.UtcNow.Add(leaseDuration);

                try
                {
                    await entityContext.SaveChangesAsync();
                    return LeaseResult.Leased(lease);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return LeaseResult.NotLeased();
                }
            }
        }

        public async Task<LeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                var lease = await entityContext
                    .Leases
                    .Where(x => x.Name == name)
                    .FirstOrDefaultAsync();

                if (lease == null)
                {
                    lease = new LeaseEntity
                    {
                        Name = name,
                        End = DateTimeOffset.UtcNow.Add(leaseDuration),
                    };
                    entityContext.Leases.Add(lease);

                    try
                    {
                        await entityContext.SaveChangesAsync();
                        return LeaseResult.Leased(lease);
                    }
                    catch (Exception ex) when (entityContext.IsUniqueConstraintViolationException(ex))
                    {
                        return LeaseResult.NotLeased();
                    }
                }
                else
                {
                    if (lease.End.HasValue && lease.End.Value > DateTimeOffset.UtcNow)
                    {
                        return LeaseResult.NotLeased();
                    }

                    lease.End = DateTimeOffset.UtcNow.Add(leaseDuration);

                    try
                    {
                        await entityContext.SaveChangesAsync();
                        return LeaseResult.Leased(lease);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        return LeaseResult.NotLeased();
                    }
                }
            }
        }
    }
}
