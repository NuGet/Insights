// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
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
                // arrange
                var leaseA = await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act
                await Target.BreakAsync(LeaseName);

                // assert
                var leaseB = await Target.AcquireAsync(LeaseName, MaxDuration);
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
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act
                await Target.ReleaseAsync(leaseResultA);
                var leaseResultB = await Target.TryAcquireAsync(LeaseName, MaxDuration);

                // assert
                Assert.Equal(leaseResultA.Name, leaseResultB.Name);
                Assert.True(leaseResultA.Acquired);
                Assert.True(leaseResultB.Acquired);
            }

            [Fact]
            public async Task AllowsReleaseLeaseAfterTimeout()
            {
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MinDuration);
                await Task.Delay(MinDuration + TimeSpan.FromSeconds(1));

                // act
                var released = await Target.TryReleaseAsync(leaseResultA);

                // assert
                Assert.True(released);
            }

            [Fact]
            public async Task AllowsReleaseWithRetriesAndNoOtherThread()
            {
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);
                var requestCount = 0;
                ServiceClientFactory.HandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Headers.TryGetValues("x-ms-lease-action", out var values)
                        && values.FirstOrDefault() == "release")
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            await b(r, t);
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                            {
                                RequestMessage = r,
                            };
                        }
                    };

                    return null;
                };

                // act
                var released = await Target.TryReleaseAsync(leaseResultA);

                // assert
                Assert.True(released);
            }

            [Fact]
            public async Task DoesNotAllowReleaseWithRetriesAndAnotherThread()
            {
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);

                var requestCount = 0;
                StorageLeaseResult leaseResultB = null;
                ServiceClientFactory.HandlerFactory.OnSendAsync = async (r, b, t) =>
                {
                    if (r.Headers.TryGetValues("x-ms-lease-action", out var values)
                        && values.FirstOrDefault() == "release")
                    {
                        requestCount++;
                        if (requestCount == 1)
                        {
                            // perform the release
                            await b(r, t);

                            // make another thread acquire
                            leaseResultB = await Target.TryAcquireAsync(LeaseName, MaxDuration);

                            // signal the caller to retry
                            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                            {
                                RequestMessage = r,
                            };
                        }
                    };

                    return null;
                };

                // act
                var released = await Target.TryReleaseAsync(leaseResultA);

                // assert
                Assert.False(released);
                Assert.NotNull(leaseResultB);
                Assert.True(leaseResultB.Acquired);
            }

            [Fact]
            public async Task ReleasesLease()
            {
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act
                var released = await Target.TryReleaseAsync(leaseResultA);

                // assert
                Assert.True(released);
            }

            [Fact]
            public async Task FailsToReleaseLostLease()
            {
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MinDuration);
                await Task.Delay(MinDuration + TimeSpan.FromSeconds(1));
                await AcquireWithRetryAsync(LeaseName, MinDuration);

                // act
                var released = await Target.TryReleaseAsync(leaseResultA);

                // assert
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
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MinDuration);
                await Task.Delay(MinDuration + TimeSpan.FromSeconds(1));
                var leaseResultB = await AcquireWithRetryAsync(LeaseName, MinDuration);

                // act & assert
                var ex = await Assert.ThrowsAsync<StorageLeaseException>(
                    () => Target.ReleaseAsync(leaseResultA));
                Assert.Equal("The lease has been acquired by someone else, or transient errors happened.", ex.Message);
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
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);
                await Target.BreakAsync(LeaseName);

                // act & assert
                var ex = await Assert.ThrowsAsync<StorageLeaseException>(
                    () => Target.RenewAsync(leaseResultA));
                Assert.Equal("The lease has been acquired by someone else, or transient errors happened.", ex.Message);
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
                // arrange
                var leaseResult = await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act
                var renewed = await Target.TryRenewAsync(leaseResult);

                // assert
                Assert.True(renewed);
            }

            [Fact]
            public async Task FailsToRenewLostLease()
            {
                // arrange
                var leaseResultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);
                await Target.BreakAsync(LeaseName);
                await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act
                var renewed = await Target.TryRenewAsync(leaseResultA);

                // assert
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
                // arrange
                await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act & assert
                var ex = await Assert.ThrowsAsync<StorageLeaseException>(
                    () => Target.AcquireAsync(LeaseName, MaxDuration));
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
                // act
                var result = await Target.TryAcquireAsync(LeaseName, MaxDuration);

                // assert
                Assert.True(result.Acquired);
                Assert.NotNull(result.Lease);
                Assert.Equal(LeaseName, result.Name);
            }

            [Fact]
            public async Task DoesNotAcquireAlreadyAcquiredLease()
            {
                // arrange
                var resultA = await AcquireWithRetryAsync(LeaseName, MaxDuration);

                // act
                var resultB = await Target.TryAcquireAsync(LeaseName, MaxDuration);

                // assert
                Assert.True(resultA.Acquired);
                Assert.False(resultB.Acquired);
            }

            [Fact]
            public async Task ManyThreadsDoNotCauseException()
            {
                var sw = Stopwatch.StartNew();
                await Task.WhenAll(Enumerable
                    .Range(0, 16)
                    .Select(async x =>
                    {
                        foreach (var i in Enumerable.Range(0, 50))
                        {
                            try
                            {
                                Output.WriteLine($"[{sw.Elapsed}] [{x}] Trying to acquire lease {i}...");
                                var acquireSw = Stopwatch.StartNew();
                                var result = await Target.TryAcquireAsync(i.ToString(CultureInfo.InvariantCulture), MinDuration);
                                if (result.Acquired)
                                {
                                    Output.WriteLine($"[{sw.Elapsed}] [{x}] Releasing lease {i}...");
                                    try
                                    {
                                        await Target.ReleaseAsync(result);
                                    }
                                    catch (StorageLeaseException) when (acquireSw.Elapsed >= MinDuration)
                                    {
                                        Output.WriteLine($"[{sw.Elapsed}] [{x}] Timeout for lease {i}, failed to release.");
                                    }
                                }
                                else
                                {
                                    Output.WriteLine($"[{sw.Elapsed}] [{x}] Could not acquire lease {i}.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Output.WriteLine($"[{sw.Elapsed}] [{x}] Error: " + ex.Message.Split('\n').First().Trim());
                                throw;
                            }
                        }
                    }));
            }
        }

        public abstract class BaseTest : IAsyncLifetime
        {
            public BaseTest(ITestOutputHelper output)
            {
                Output = output;
                ContainerName = LogicTestSettings.NewStoragePrefix() + "1l1";
                LeaseName = "some-lease";
                MinDuration = TimeSpan.FromSeconds(15);
                MaxDuration = TimeSpan.FromSeconds(60);
                Settings = new NuGetInsightsSettings
                {
                    LeaseContainerName = ContainerName,
                }.WithTestStorageSettings();
                Options = new Mock<IOptions<NuGetInsightsSettings>>();
                Options.Setup(x => x.Value).Returns(() => Settings);
                HttpClientHandler = new HttpClientHandler();
                ServiceClientFactory = new TestServiceClientFactory(
                    () => new LoggingHandler(output.GetLogger<LoggingHandler>()),
                    HttpClientHandler,
                    Options.Object,
                    output.GetLoggerFactory());
                Target = new StorageLeaseService(ServiceClientFactory, Options.Object);
            }

            public ITestOutputHelper Output { get; }
            public string ContainerName { get; }
            public string LeaseName { get; }
            public TimeSpan MinDuration { get; }
            public TimeSpan MaxDuration { get; }
            public NuGetInsightsSettings Settings { get; }
            public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
            public HttpClientHandler HttpClientHandler { get; }
            public TestServiceClientFactory ServiceClientFactory { get; }
            public StorageLeaseService Target { get; }

            /// <summary>
            /// This should only be used in the "arrange" or setup step of a unit test.
            /// </summary>
            public async Task<StorageLeaseResult> AcquireWithRetryAsync(string lease, TimeSpan leaseDuration)
            {
                const int maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        return await Target.AcquireAsync(lease, leaseDuration);
                    }
                    catch (StorageLeaseException ex) when (attempt < maxAttempts)
                    {
                        Output.GetLogger<BaseTest>().LogTransientWarning(ex, "Failed to acquire lease. Trying again.");
                    }
                }

                throw new NotImplementedException();
            }

            /// <summary>
            /// The tests in this test suite are flaky due to the timing nature of a blob lease.
            /// </summary>
            public async Task RetryAsync(Func<Task> testAsync)
            {
                const int maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        await testAsync();
                    }
                    catch (StorageLeaseException ex) when (attempt < maxAttempts)
                    {
                        Output.GetLogger<BaseTest>().LogTransientWarning(ex, "[Attempt {Attempt}] A retriable exception was thrown. Retrying.", attempt);
                    }
                }
            }

            public async Task DisposeAsync()
            {
                Output.WriteTestCleanup();

                await (await ServiceClientFactory.GetBlobServiceClientAsync())
                    .GetBlobContainerClient(ContainerName)
                    .DeleteIfExistsAsync();

                HttpClientHandler.Dispose();
            }

            public async Task InitializeAsync()
            {
                await Target.InitializeAsync();
            }
        }
    }
}
