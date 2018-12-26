using System;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Logic
{
    public class LeaseServiceTest
    {
        public class GetAsync : BaseTest
        {
            public GetAsync(ITestOutputHelper output) : base(output)
            {
            }
            
            [Fact]
            public async Task ReturnsNullLeaseWhenDoesNotExist()
            {
                var lease = await Target.GetOrNullAsync(LeaseName);

                Assert.Null(lease);
            }

            [Fact]
            public async Task ReturnsLeaseWhenExists()
            {
                LeaseEntity leaseA;
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    leaseA = new LeaseEntity
                    {
                        Name = LeaseName,
                        End = DateTimeOffset.UtcNow.AddMinutes(-1),
                    };
                    entityContext.Leases.Add(leaseA);
                    await entityContext.SaveChangesAsync();
                }

                var leaseB = await Target.GetOrNullAsync(LeaseName);

                Assert.NotNull(leaseB);
                Assert.NotSame(leaseA, leaseB);
                Assert.Equal(leaseA.Name, leaseB.Name);
                Assert.Equal(leaseA.End, leaseB.End);
                Assert.Equal(leaseA.LeaseKey, leaseB.LeaseKey);
                Assert.NotEqual(default(long), leaseB.LeaseKey);
                Assert.Equal(leaseA.RowVersion, leaseB.RowVersion);
                Assert.NotNull(leaseB.RowVersion);
            }
        }

        public class ReleaseAsync : BaseTest
        {
            public ReleaseAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsReleaseLeaseToBeReacquired()
            {
                var leaseResultA = await Target.TryAcquireAsync(LeaseName, Duration);

                await Target.TryReleaseAsync(leaseResultA.Lease);
                var leaseResultB = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.Equal(leaseResultA.Lease.LeaseKey, leaseResultB.Lease.LeaseKey);
                Assert.True(leaseResultA.Acquired);
                Assert.True(leaseResultB.Acquired);
                Assert.Null(leaseResultA.Lease.End);
                Assert.NotNull(leaseResultB.Lease.End);
            }

            [Fact]
            public async Task ReleasesLease()
            {
                var leaseResultA = await Target.TryAcquireAsync(LeaseName, Duration);

                var leaseResultB = await Target.TryReleaseAsync(leaseResultA.Lease);

                Assert.False(leaseResultB.Acquired);
                Assert.Null(leaseResultB.Lease);
            }

            [Fact]
            public async Task FailsToReleaseLostLease()
            {
                var leaseResultA = await Target.TryAcquireAsync(LeaseName, Duration);
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var lease = await entityContext.Leases.SingleAsync(x => x.Name == LeaseName);
                    lease.End = lease.End.Value.Add(Duration);
                    await entityContext.SaveChangesAsync();
                }

                var leaseResultB = await Target.TryReleaseAsync(leaseResultA.Lease);

                Assert.False(leaseResultB.Acquired);
                Assert.NotSame(leaseResultA.Lease, leaseResultB.Lease);
            }
        }

        public class RenewAsync : BaseTest
        {
            public RenewAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task RenewsWhenLeaseIsStillActive()
            {
                var leaseResult = await Target.TryAcquireAsync(LeaseName, TimeSpan.Zero);

                var before = DateTimeOffset.UtcNow;
                await Target.RenewAsync(leaseResult.Lease, Duration);
                var after = DateTimeOffset.UtcNow;

                Assert.InRange(leaseResult.Lease.End.Value, before.Add(Duration), after.Add(Duration));
            }

            [Fact]
            public async Task FailsToRenewLostLease()
            {
                var leaseResultA = await Target.TryAcquireAsync(LeaseName, Duration);
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var lease = await entityContext.Leases.SingleAsync(x => x.Name == LeaseName);
                    lease.End = lease.End.Value.Add(Duration);
                    await entityContext.SaveChangesAsync();
                }

                var leaseResultB = await Target.RenewAsync(leaseResultA.Lease, Duration);

                Assert.False(leaseResultB.Acquired);
                Assert.NotSame(leaseResultA.Lease, leaseResultB.Lease);
            }
        }

        public class TryAcquireAsync : BaseTest
        {
            public TryAcquireAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AcquiresWhenNotExists()
            {
                var before = DateTimeOffset.UtcNow;
                var result = await Target.TryAcquireAsync(LeaseName, Duration);
                var after = DateTimeOffset.UtcNow;

                Assert.True(result.Acquired);
                Assert.NotNull(result.Lease);
                Assert.Equal(LeaseName, result.Lease.Name);
                Assert.NotNull(result.Lease.End);
                Assert.InRange(result.Lease.End.Value, before.Add(Duration), after.Add(Duration));
            }

            [Fact]
            public async Task AcquiresWhenEndIsInPast()
            {
                LeaseEntity lease;
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    lease = new LeaseEntity
                    {
                        Name = LeaseName,
                        End = DateTimeOffset.UtcNow.AddMinutes(-1),
                    };
                    entityContext.Leases.Add(lease);
                    await entityContext.SaveChangesAsync();
                }

                var before = DateTimeOffset.UtcNow;
                var result = await Target.TryAcquireAsync(LeaseName, Duration);
                var after = DateTimeOffset.UtcNow;

                Assert.True(result.Acquired);
                Assert.NotNull(result.Lease);
                Assert.Equal(LeaseName, result.Lease.Name);
                Assert.NotNull(result.Lease.End);
                Assert.InRange(result.Lease.End.Value, before.Add(Duration), after.Add(Duration));
            }

            [Fact]
            public async Task DoesNotAcquireWhenEndIsFuture()
            {
                var end = DateTimeOffset.UtcNow.AddHours(1);
                LeaseEntity lease;
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    lease = new LeaseEntity
                    {
                        Name = LeaseName,
                        End = end,
                    };
                    entityContext.Leases.Add(lease);
                    await entityContext.SaveChangesAsync();
                }

                var result = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.False(result.Acquired);
                Assert.Null(result.Lease);
            }

            [Fact]
            public async Task DoesNotAcquireAlreadyAcquiredLease()
            {
                var resultA = await Target.TryAcquireAsync(LeaseName, Duration);
                var resultB = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.True(resultA.Acquired);
                Assert.False(resultB.Acquired);
            }

            [Fact]
            public async Task DoesNotAcquireWhenSomeoneElseLeasesFirst()
            {
                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var lease = new LeaseEntity
                    {
                        Name = LeaseName,
                        End = DateTimeOffset.UtcNow.AddMinutes(-1),
                    };
                    entityContext.Leases.Add(lease);
                    await entityContext.SaveChangesAsync();
                }

                var end = DateTimeOffset.UtcNow.AddHours(1);
                ExecuteBeforeCommitAsync = async () =>
                {
                    using (var entityContext = await UnhookedEntityContextFactory.GetAsync())
                    {
                        var lease = await entityContext
                            .Leases
                            .Where(x => x.Name == LeaseName)
                            .FirstAsync();
                        lease.End = end;
                        var changes = await entityContext.SaveChangesAsync();
                    }
                };

                var result = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.False(result.Acquired);
                Assert.Null(result.Lease);
            }

            [Fact]
            public async Task DoesNotAcquireWhenSomeoneElseAddsTheLeaseFirst()
            {
                var end = DateTimeOffset.UtcNow.AddHours(1);
                ExecuteBeforeCommitAsync = async () =>
                {
                    using (var entityContext = await UnhookedEntityContextFactory.GetAsync())
                    {
                        entityContext.Leases.Add(new LeaseEntity
                        {
                            Name = LeaseName,
                            End = end,
                        });
                        await entityContext.SaveChangesAsync();
                    }
                };

                var result = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.False(result.Acquired);
                Assert.Null(result.Lease);
            }
        }

        public abstract class BaseTest : BaseDatabaseTest
        {
            public BaseTest(ITestOutputHelper output) : base(output)
            {
                LeaseName = "test-lease";
                Duration = TimeSpan.FromMinutes(10);

                Target = new LeaseService(EntityContextFactory);
            }

            public string LeaseName { get; }
            public TimeSpan Duration { get; set; }
            public LeaseService Target { get; }
        }
    }
}
