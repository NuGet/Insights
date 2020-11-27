using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class DatabaseLeaseService : IDatabaseLeaseService
    {
        private readonly ICommitCondition _commitCondition;
        private readonly EntityContextFactory _entityContextFactory;

        public DatabaseLeaseService(
            ICommitCondition commitCondition,
            EntityContextFactory entityContextFactory)
        {
            _commitCondition = commitCondition;
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

        public async Task BreakAsync(string name)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            using (var connection = entityContext.Database.GetDbConnection())
            {
                await _commitCondition.VerifyAsync();
                await connection.OpenAsync();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE Leases SET [End] = NULL WHERE Name = @Name";

                    var nameParameter = command.CreateParameter();
                    nameParameter.ParameterName = "Name";
                    nameParameter.DbType = DbType.String;
                    nameParameter.Value = name;
                    command.Parameters.Add(nameParameter);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task ReleaseAsync(LeaseEntity lease)
        {
            await TryReleaseAsync(lease, shouldThrow: true);
        }

        public async Task<bool> TryReleaseAsync(LeaseEntity lease)
        {
            return await TryReleaseAsync(lease, shouldThrow: false);
        }

        private async Task<bool> TryReleaseAsync(LeaseEntity lease, bool shouldThrow)
        {
            if (lease.End == null)
            {
                if (shouldThrow)
                {
                    throw new ArgumentException(DatabaseLeaseResult.NotAcquiredAtAll, nameof(lease));
                }
                else
                {
                    return false;
                }
            }

            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                entityContext.Leases.Attach(lease);
                lease.End = null;

                try
                {
                    await entityContext.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (shouldThrow)
                    {
                        throw new InvalidOperationException(DatabaseLeaseResult.AcquiredBySomeoneElse, ex);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        public async Task RenewAsync(LeaseEntity lease, TimeSpan leaseDuration)
        {
            await TryRenewAsync(lease, leaseDuration, shouldThrow: true);
        }

        public async Task<bool> TryRenewAsync(LeaseEntity lease, TimeSpan leaseDuration)
        {
            return await TryRenewAsync(lease, leaseDuration, shouldThrow: false);
        }

        private async Task<bool> TryRenewAsync(LeaseEntity lease, TimeSpan leaseDuration, bool shouldThrow)
        {
            if (lease.End == null)
            {
                if (shouldThrow)
                {
                    throw new ArgumentException(DatabaseLeaseResult.NotAcquiredAtAll, nameof(lease));
                }
                else
                {
                    return false;
                }
            }

            using (var entityContext = await _entityContextFactory.GetAsync())
            {
                entityContext.Leases.Attach(lease);
                lease.End = DateTimeOffset.UtcNow.Add(leaseDuration);

                try
                {
                    await entityContext.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    if (shouldThrow)
                    {
                        throw new InvalidOperationException(DatabaseLeaseResult.AcquiredBySomeoneElse, ex);
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        public async Task<LeaseEntity> AcquireAsync(string name, TimeSpan leaseDuration)
        {
            var result = await TryAcquireAsync(name, leaseDuration, shouldThrow: true);
            return result.Lease;
        }

        public async Task<DatabaseLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration)
        {
            return await TryAcquireAsync(name, leaseDuration, shouldThrow: false);
        }

        private async Task<DatabaseLeaseResult> TryAcquireAsync(string name, TimeSpan leaseDuration, bool shouldThrow)
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
                        return DatabaseLeaseResult.Leased(lease);
                    }
                    catch (Exception ex) when (entityContext.IsUniqueConstraintViolationException(ex))
                    {
                        if (shouldThrow)
                        {
                            throw new InvalidOperationException(DatabaseLeaseResult.NotAvailable, ex);
                        }
                        else
                        {
                            return DatabaseLeaseResult.NotLeased();
                        }
                    }
                }
                else
                {
                    if (lease.End.HasValue && lease.End.Value > DateTimeOffset.UtcNow)
                    {
                        if (shouldThrow)
                        {
                            throw new InvalidOperationException(DatabaseLeaseResult.NotAvailable);
                        }
                        else
                        {
                            return DatabaseLeaseResult.NotLeased();
                        }
                    }

                    lease.End = DateTimeOffset.UtcNow.Add(leaseDuration);

                    try
                    {
                        await entityContext.SaveChangesAsync();
                        return DatabaseLeaseResult.Leased(lease);
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        if (shouldThrow)
                        {
                            throw new InvalidOperationException(DatabaseLeaseResult.NotAvailable, ex);
                        }
                        else
                        {
                            return DatabaseLeaseResult.NotLeased();
                        }
                    }
                }
            }
        }
    }
}
