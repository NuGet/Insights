using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Logic
{
    public class SingletonServiceTest
    {
        public class IntegrationTests : BaseTest
        {
            public IntegrationTests(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task CanAcquireRenewThenRelease()
            {
                await TargetA.AcquireOrRenewAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(() => TargetB.AcquireOrRenewAsync());

                await TargetA.RenewAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(() => TargetB.AcquireOrRenewAsync());

                await TargetA.ReleaseInAsync(TimeSpan.Zero);

                await Task.Delay(TimeSpan.FromMilliseconds(1));

                await TargetB.AcquireOrRenewAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(() => TargetA.AcquireOrRenewAsync());
            }

            [Fact]
            public async Task CanAcquireReleaseThenAcquire()
            {
                await TargetA.AcquireOrRenewAsync();

                await TargetA.ReleaseInAsync(TimeSpan.Zero);

                await TargetA.AcquireOrRenewAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(() => TargetB.AcquireOrRenewAsync());
            }

            [Fact]
            public async Task CanAcquireTimeoutThenAcquire()
            {
                await TargetA.AcquireOrRenewAsync();

                using (var entityContext = await EntityContextFactory.GetAsync())
                {
                    var lease = await entityContext.Leases.SingleAsync(x => x.Name == SingletonService.LeaseName);
                    lease.End = lease.End = null;
                    await entityContext.SaveChangesAsync();
                }

                await Assert.ThrowsAsync<InvalidOperationException>(() => TargetA.AcquireOrRenewAsync());

                await TargetA.AcquireOrRenewAsync();

                await Assert.ThrowsAsync<InvalidOperationException>(() => TargetB.AcquireOrRenewAsync());
            }
        }


        public abstract class BaseTest : BaseDatabaseTest
        {
            public BaseTest(ITestOutputHelper output) : base(output)
            {
                Duration = TimeSpan.FromMinutes(10);
                LeaseService = new LeaseService(
                    NullCommitCondition.Instance,
                    EntityContextFactory);
                Logger = output.GetLogger<SingletonService>();

                TargetA = new SingletonService(LeaseService, Logger);
                TargetB = new SingletonService(LeaseService, Logger);
            }

            public TimeSpan Duration { get; set; }
            public LeaseService LeaseService { get; }
            public ILogger<SingletonService> Logger { get; }
            public SingletonService TargetA { get; }
            public SingletonService TargetB { get; }
        }
    }
}
