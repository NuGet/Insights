using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages
{
    public class StorageLeaseServiceTest
    {
        public class BreakAsync : BaseTest
        {
            public BreakAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsSomeoneElseToAcquire()
            {
                var leaseA = await Target.AcquireAsync(LeaseName, Duration);

                await Target.BreakAsync(LeaseName);

                var leaseB = await Target.AcquireAsync(LeaseName, Duration);
                Assert.Equal(leaseA.Name, leaseB.Name);
                Assert.NotEqual(leaseA.Lease, leaseB.Lease);
            }
        }

        public class TryReleaseAsync : BaseTest
        {
            public TryReleaseAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task AllowsReleaseLeaseToBeReacquired()
            {
                var leaseResultA = await Target.AcquireAsync(LeaseName, Duration);

                await Target.ReleaseAsync(leaseResultA);
                var leaseResultB = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.Equal(leaseResultA.Name, leaseResultB.Name);
                Assert.True(leaseResultA.Acquired);
                Assert.True(leaseResultB.Acquired);
            }

            [Fact]
            public async Task ReleasesLease()
            {
                var leaseResultA = await Target.AcquireAsync(LeaseName, Duration);

                var released = await Target.TryReleaseAsync(leaseResultA);

                Assert.True(released);
            }

            [Fact]
            public async Task FailsToReleaseLostLease()
            {
                var leaseResultA = await Target.AcquireAsync(LeaseName, TimeSpan.FromSeconds(15));
                await Task.Delay(TimeSpan.FromSeconds(15));
                await Target.AcquireAsync(LeaseName, TimeSpan.FromSeconds(15));

                var released = await Target.TryReleaseAsync(leaseResultA);

                Assert.False(released);
            }
        }

        public class ReleaseAsync : BaseTest
        {
            public ReleaseAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task FailsToReleaseLostLease()
            {
                var leaseResultA = await Target.AcquireAsync(LeaseName, TimeSpan.FromSeconds(15));
                await Task.Delay(TimeSpan.FromSeconds(15));
                var leaseResultB = await Target.AcquireAsync(LeaseName, TimeSpan.FromSeconds(15));

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.ReleaseAsync(leaseResultA));
                Assert.Equal("The lease has been acquired by someone else.", ex.Message);
            }
        }

        public class RenewAsync : BaseTest
        {
            public RenewAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task FailsToRenewLostLease()
            {
                var leaseResultA = await Target.AcquireAsync(LeaseName, Duration);
                await Target.BreakAsync(LeaseName);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.RenewAsync(leaseResultA));
                Assert.Equal("The lease has been acquired by someone else.", ex.Message);
            }
        }

        public class TryRenewAsync : BaseTest
        {
            public TryRenewAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task RenewsWhenLeaseIsStillActive()
            {
                var leaseResult = await Target.AcquireAsync(LeaseName, TimeSpan.FromSeconds(15));

                var renewed = await Target.TryRenewAsync(leaseResult);

                Assert.True(renewed);
            }

            [Fact]
            public async Task FailsToRenewLostLease()
            {
                var leaseResultA = await Target.AcquireAsync(LeaseName, Duration);
                await Target.BreakAsync(LeaseName);
                await Target.AcquireAsync(LeaseName, Duration);

                var renewed = await Target.TryRenewAsync(leaseResultA);

                Assert.False(renewed);
            }
        }

        public class AcquireAsync : BaseTest
        {
            public AcquireAsync(ITestOutputHelper output) : base(output)
            {
            }

            [Fact]
            public async Task DoesNotAcquireWhenSomeoneElseLeasesFirst()
            {
                await Target.AcquireAsync(LeaseName, Duration);

                var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => Target.AcquireAsync(LeaseName, Duration));
                Assert.Equal("The lease is not available yet.", ex.Message);
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
                var result = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.True(result.Acquired);
                Assert.NotNull(result.Lease);
                Assert.Equal(LeaseName, result.Name);
            }

            [Fact]
            public async Task DoesNotAcquireAlreadyAcquiredLease()
            {
                var resultA = await Target.AcquireAsync(LeaseName, Duration);

                var resultB = await Target.TryAcquireAsync(LeaseName, Duration);

                Assert.True(resultA.Acquired);
                Assert.False(resultB.Acquired);
            }
        }

        public abstract class BaseTest : IAsyncLifetime
        {
            public BaseTest(ITestOutputHelper output)
            {
                ContainerName = Guid.NewGuid().ToString("N");
                LeaseName = "some-lease";
                Duration = TimeSpan.FromSeconds(60);
                Settings = new ExplorePackagesSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                    LeaseContainerName = ContainerName,
                };
                Options = new Mock<IOptions<ExplorePackagesSettings>>();
                Options.Setup(x => x.Value).Returns(() => Settings);
                ServiceClientFactory = new ServiceClientFactory(Options.Object);
                Target = new StorageLeaseService(ServiceClientFactory, Options.Object);
            }

            public string ContainerName { get; }
            public string LeaseName { get; }
            public TimeSpan Duration { get; }
            public ExplorePackagesSettings Settings { get; }
            public Mock<IOptions<ExplorePackagesSettings>> Options { get; }
            public ServiceClientFactory ServiceClientFactory { get; }
            public StorageLeaseService Target { get; }

            public async Task DisposeAsync()
            {
                await ServiceClientFactory
                    .GetStorageAccount()
                    .CreateCloudBlobClient()
                    .GetContainerReference(ContainerName)
                    .DeleteIfExistsAsync();
            }

            public async Task InitializeAsync()
            {
                await Target.InitializeAsync();
            }
        }
    }
}
