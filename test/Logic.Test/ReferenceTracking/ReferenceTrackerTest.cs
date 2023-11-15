// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Nito.AsyncEx;
using NuGet.Insights.StorageNoOpRetry;
using NuGet.Insights.WideEntities;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Insights.ReferenceTracking
{
    public class ReferenceTrackerTest : IClassFixture<ReferenceTrackerTest.Fixture>
    {
        private const string OwnerTypeSuffix = "OwnerType";

        private const string OwnerPK0 = nameof(OwnerPK0);

        private const string OwnerRK0 = nameof(OwnerRK0);
        private const string OwnerRK1 = nameof(OwnerRK1);
        private const string OwnerRK2 = nameof(OwnerRK2);
        private const string OwnerRK3 = nameof(OwnerRK3);
        private const string OwnerRK4 = nameof(OwnerRK4);
        private const string OwnerRK5 = nameof(OwnerRK5);

        private const string OwnerPK1 = nameof(OwnerPK1);

        private const string OwnerRK6 = nameof(OwnerRK6);
        private const string OwnerRK7 = nameof(OwnerRK7);
        private const string OwnerRK8 = nameof(OwnerRK8);
        private const string OwnerRK9 = nameof(OwnerRK9);

        private const string SubjectTypeSuffix = "SubjectType";

        private const string SubjectPK0 = nameof(SubjectPK0);

        private const string SubjectRK0 = nameof(SubjectRK0);
        private const string SubjectRK1 = nameof(SubjectRK1);
        private const string SubjectRK2 = nameof(SubjectRK2);
        private const string SubjectRK3 = nameof(SubjectRK3);
        private const string SubjectRK4 = nameof(SubjectRK4);
        private const string SubjectRK5 = nameof(SubjectRK5);

        private const string SubjectPK1 = nameof(SubjectPK1);

        private const string SubjectRK6 = nameof(SubjectRK6);
        private const string SubjectRK7 = nameof(SubjectRK7);
        private const string SubjectRK8 = nameof(SubjectRK8);
        private const string SubjectRK9 = nameof(SubjectRK9);

        public class InterleavedOperation_0 : BaseInterleavedOperation
        {
            public InterleavedOperation_0(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 0);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_1 : BaseInterleavedOperation
        {
            public InterleavedOperation_1(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 1);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_2 : BaseInterleavedOperation
        {
            public InterleavedOperation_2(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 2);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_3 : BaseInterleavedOperation
        {
            public InterleavedOperation_3(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 3);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_4 : BaseInterleavedOperation
        {
            public InterleavedOperation_4(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 4);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_5 : BaseInterleavedOperation
        {
            public InterleavedOperation_5(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 5);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_6 : BaseInterleavedOperation
        {
            public InterleavedOperation_6(Fixture fixture, ITestOutputHelper output) : base(fixture, output) {}
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 6);
            [Theory(Skip = "Too slow.")] [MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        public class InterleavedOperation_7 : BaseInterleavedOperation
        {
            public InterleavedOperation_7(Fixture fixture, ITestOutputHelper output) : base(fixture, output) { }
            public static IEnumerable<object[]> TestData => AllTestData.Where((x, i) => i % ParallelTestCount == 7);
            [Theory(Skip = "Too slow.")][MemberData(nameof(TestData))] public Task Execute(string desiredOrder) => TestAsync(desiredOrder);
        }

        /// <summary>
        /// Test parallelization idea: https://stackoverflow.com/a/34828001
        /// </summary>
        public abstract class BaseInterleavedOperation : ReferenceTrackerTest
        {
            private const int RequestCount = 7;
            protected static IEnumerable<object[]> AllTestData => IterTools
                .Interleave(
                    Enumerable.Repeat('a', RequestCount).ToList(),
                    Enumerable.Repeat('b', RequestCount).ToList())
                .Select(x => new string(x.ToArray()))
                // This removes one of ['aaaaaabbbbbb', 'bbbbbbaaaaaa'] since there are logically equivalent for the test input.
                .Select(x => x[0] == 'b' ? x.Replace('b', 'x').Replace('a', 'b').Replace('x', 'a') : x)
                .Distinct()
                .OrderBy(x => x, StringComparer.Ordinal)
                .Select(x => new object[] { x });

            protected const int ParallelTestCount = 8;

            protected async Task TestAsync(string desiredOrder)
            {
                var referencesInitial = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1) } },
                };
                await SetReferencesAsync(referencesInitial);

                var referencesA = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } },
                };
                var referencesB = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                };

                var orderQueue = new Queue<char>(desiredOrder);
                var actualOrder = new List<char>();
                var requestLock = new SemaphoreSlim(1);
                var resetEvent = new AsyncAutoResetEvent(set: true);

                async Task<HttpResponseMessage> WaitForTurnAsync(char id, HttpRequestMessage req, SendMessageAsync sendMessageAsync, CancellationToken token)
                {
                    while (true)
                    {
                        await requestLock.WaitAsync();
                        try
                        {
                            bool waiting = true;
                            if (orderQueue.Count == 0)
                            {
                                waiting = false;
                            }
                            else if (orderQueue.Peek() == id)
                            {
                                waiting = false;
                                orderQueue.Dequeue();
                            }

                            if (!waiting)
                            {
                                var response = await sendMessageAsync(req, token);
                                resetEvent.Set();
                                if (response.StatusCode < HttpStatusCode.InternalServerError)
                                {
                                    actualOrder.Add(id);
                                }
                                Output.WriteLine($"[{id}] [resp ] {req.Method} -> {(int)response.StatusCode}");
                                return response;
                            }
                        }
                        finally
                        {
                            requestLock.Release();
                        }

                        await resetEvent.WaitAsync();
                    }
                }

                async Task<Exception> ExecuteAsync(char id, ReferenceTracker target, IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> references)
                {
                    Exception output;
                    try
                    {
                        Output.WriteLine($"[{id}] [start]");
                        await target.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, references);
                        Output.WriteLine($"[{id}] [done ]");
                        output = null;
                    }
                    catch (Exception ex)
                    {
                        Output.WriteLine($"[{id}] [error]");
                        output = ex;
                    }

                    await requestLock.WaitAsync();
                    try
                    {
                        orderQueue.Clear();
                        resetEvent.Set();
                    }
                    finally
                    {
                        requestLock.Release();
                    }

                    return output;
                }

                ServiceClientFactoryA.HandlerFactory.Clear();
                ServiceClientFactoryB.HandlerFactory.Clear();

                ServiceClientFactoryA.HandlerFactory.OnSendAsync = (r, b, t) => WaitForTurnAsync('a', r, b, t);
                ServiceClientFactoryB.HandlerFactory.OnSendAsync = (r, b, t) => WaitForTurnAsync('b', r, b, t);

                var exceptions = await Task.WhenAll(
                    ExecuteAsync('a', TargetA, referencesA),
                    ExecuteAsync('b', TargetB, referencesB));

                Output.WriteLine($"Desired order: {desiredOrder}");
                Output.WriteLine($"Actual order:  {new string(actualOrder.ToArray())}");

                var idToDesiredRequestCount = desiredOrder.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
                var idToActualRequestCount = actualOrder.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());

                var errorMessage = $"There are more actual requests than desired requests. Increase {nameof(BaseInterleavedOperation)}.{nameof(RequestCount)}.";
                Assert.True(idToActualRequestCount['a'] <= idToDesiredRequestCount['a'], errorMessage);
                Assert.True(idToActualRequestCount['b'] <= idToDesiredRequestCount['b'], errorMessage);

                Assert.Equal(2, exceptions.Length);
                Assert.True(exceptions.Count(x => x != null) <= 1);

                if (exceptions[0] != null)
                {
                    Output.WriteLine($"[a] [retry]");
                    await ExecuteAsync('a', TargetA, referencesA);
                    await AssertExpectedAsync(referencesA);
                }
                else if (exceptions[1] != null)
                {
                    Output.WriteLine($"[b] [retry]");
                    await ExecuteAsync('b', TargetB, referencesB);
                    await AssertExpectedAsync(referencesB);
                }
                else if (actualOrder.Last() == 'a')
                {
                    await AssertExpectedAsync(referencesA);
                }
                else if (actualOrder.Last() == 'b')
                {
                    await AssertExpectedAsync(referencesB);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            public BaseInterleavedOperation(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class FaultInjection : ReferenceTrackerTest, IClassFixture<FaultInjection.FaultInjectionFixture>
        {
            private const int SetReferencesAsync_IsEventuallyConsistent_WithSameInput_Max = 10;
            public static IEnumerable<object[]> SetReferencesAsync_IsEventuallyConsistent_WithSameInput_TestData => Enumerable
                .Range(1, SetReferencesAsync_IsEventuallyConsistent_WithSameInput_Max)
                .Select(x => new object[] { x });

            [Theory(Skip = "This test is a little bit flaky.")]
            [MemberData(nameof(SetReferencesAsync_IsEventuallyConsistent_WithSameInput_TestData))]
            public async Task SetReferencesAsync_IsEventuallyConsistent_WithSameInput(int failAtRequest)
            {
                // Arrange
                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK4, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK8) } },
                };

                await ExecuteWithFaultAsync(failAtRequest, references);

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                _faultInjectionFixture.SetReferencesAsync_IsEventuallyConsistent_WithSameInput_HasFault.GetOrAdd(failAtRequest, HasFault);
            }

            private const int SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_Max = 10;
            public static IEnumerable<object[]> SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_TestData => Enumerable
                .Range(1, SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_Max)
                .Select(x => new object[] { x });

            [Theory(Skip = "This test is a little bit flaky.")]
            [MemberData(nameof(SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_TestData))]
            public async Task SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll(int failAtRequest)
            {
                // Arrange
                await ExecuteWithFaultAsync(failAtRequest, new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK4, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK8) } },
                });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, ReferenceTracker.EmptySet },
                    { OwnerRK1, ReferenceTracker.EmptySet },
                    { OwnerRK2, ReferenceTracker.EmptySet },
                    { OwnerRK3, ReferenceTracker.EmptySet },
                    { OwnerRK4, ReferenceTracker.EmptySet },
                    { OwnerRK5, ReferenceTracker.EmptySet },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                _faultInjectionFixture.SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_HasFault.GetOrAdd(failAtRequest, HasFault);
            }

            private const int SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_Max = 10;
            public static IEnumerable<object[]> SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_TestData => Enumerable
                .Range(1, SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_Max)
                .Select(x => new object[] { x });

            [Theory(Skip = "This test is a little bit flaky.")]
            [MemberData(nameof(SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_TestData))]
            public async Task SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput(int failAtRequest)
            {
                // Arrange
                await ExecuteWithFaultAsync(failAtRequest, new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK4, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK8) } },
                });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK4, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK8) } },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                _faultInjectionFixture.SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_HasFault.GetOrAdd(failAtRequest, HasFault);
            }

            private const int SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_Max = 14;
            public static IEnumerable<object[]> SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_TestData => Enumerable
                .Range(1, SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_Max)
                .Select(x => new object[] { x });

            [Theory(Skip = "This test is a little bit flaky.")]
            [MemberData(nameof(SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_TestData))]
            public async Task SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting(int failAtRequest)
            {
                // Arrange
                await SetReferencesAsync(new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK4, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK3) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7), SE(SubjectPK1, SubjectRK8), SE(SubjectPK1, SubjectRK9) } },
                });

                await ExecuteWithFaultAsync(failAtRequest, new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK9) } },
                });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK4, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK5, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK8) } },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                _faultInjectionFixture.SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_HasFault.GetOrAdd(failAtRequest, HasFault);
            }

            private async Task ExecuteWithFaultAsync(int failAtRequest, Dictionary<string, IReadOnlySet<SubjectEdge>> references)
            {
                ServiceClientFactoryA.HandlerFactory.OnSendAsync = (req, _, _) =>
                {
                    if (Interlocked.Increment(ref RequestCount) == failAtRequest)
                    {
                        Output.WriteLine($"Failing request {RequestCount}.");
                        throw new InvalidOperationException("This request failed due to fault injection.");
                    }

                    Output.WriteLine($"Passing request {RequestCount}.");
                    return Task.FromResult<HttpResponseMessage>(null);
                };

                // Set references with fault injection enabled.
                try
                {
                    await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, references);
                    Output.WriteLine($"No requests failed.");
                    HasFault = false;
                }
                catch (InvalidOperationException)
                {
                    Output.WriteLine($"Failed at request {failAtRequest}.");
                    HasFault = true;
                }

                ServiceClientFactoryA.HandlerFactory.OnSendAsync = null;
                ServiceClientFactoryA.HandlerFactory.Clear();
            }

            private readonly FaultInjectionFixture _faultInjectionFixture;

            public FaultInjection(FaultInjectionFixture faultInjectionFixture, Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
                _faultInjectionFixture = faultInjectionFixture;
            }

            public int RequestCount;
            public bool HasFault;

            public class FaultInjectionFixture : IDisposable
            {
                public ConcurrentDictionary<int, bool> SetReferencesAsync_IsEventuallyConsistent_WithSameInput_HasFault { get; } = new();
                public ConcurrentDictionary<int, bool> SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_HasFault { get; } = new();
                public ConcurrentDictionary<int, bool> SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_HasFault { get; } = new();
                public ConcurrentDictionary<int, bool> SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_HasFault { get; } = new();

                public void Dispose()
                {
                    // Only execute this assertion if all of the test data has been run. This won't be the case if the
                    // Visual Studio Test Explorer was used to run a single test case.
                    VerifyTest(
                        SetReferencesAsync_IsEventuallyConsistent_WithSameInput_HasFault,
                        SetReferencesAsync_IsEventuallyConsistent_WithSameInput_Max);
                    VerifyTest(
                        SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_HasFault,
                        SetReferencesAsync_IsEventuallyConsistent_WithDeleteAll_Max);
                    VerifyTest(
                        SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_HasFault,
                        SetReferencesAsync_IsEventuallyConsistent_WithDifferentInput_Max);
                    VerifyTest(
                        SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_HasFault,
                        SetReferencesAsync_IsEventuallyConsistent_WithUpdateExisting_Max);
                }

                private void VerifyTest(ConcurrentDictionary<int, bool> hasFault, int max)
                {
                    if (hasFault.Count == max)
                    {
                        var noFaultCount = hasFault.Count(x => !x.Value);
                        Assert.True(noFaultCount == 1, noFaultCount == 0
                            ? $"Set {nameof(max)} to a higher value. Try doubling the value. There is not enough test data."
                            : $"Update {nameof(max)} to be {(max - noFaultCount) + 1}. There is too much test data.");
                    }
                }
            }
        }

        public class TheGetReferencesToSubjectAsyncMethod : ReferenceTrackerTest
        {
            [Fact]
            public async Task ReturnsNothingForNoSubjects()
            {
                var result = await TargetA.GetOwnersOfSubjectAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new SubjectReference(SubjectPK0, SubjectRK0));

                Assert.Empty(result);
            }

            [Fact]
            public async Task ReturnsSetReferences()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>>()
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } }
                });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK1, new Dictionary<string, IReadOnlySet<SubjectEdge>>()
                {
                    { OwnerRK6, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK7) } },
                    { OwnerRK7, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1) } }
                });

                // Act
                var result = await TargetA.GetOwnersOfSubjectAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new SubjectReference(SubjectPK0, SubjectRK1));

                // Assert
                Assert.Equal(
                    new OwnerReference[] { new(OwnerPK0, OwnerRK0), new(OwnerPK0, OwnerRK1), new(OwnerPK1, OwnerRK7) },
                    result);
            }

            [Fact]
            public async Task MatchesBasedOnTypesAndSubjectIdentity()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>>() { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, { OwnerRK0 + "-suffix", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0 + "-suffix", SubjectRK0) } }, { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0 + "-suffix") } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType + "-suffix", SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>>()
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, // mismatch: wrong owner type
                });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType + "-suffix", OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>>()
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, // mismatch: wrong subject type
                });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0 + "-suffix", new Dictionary<string, IReadOnlySet<SubjectEdge>>()
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, // match
                });

                // Act
                var result = await TargetA.GetOwnersOfSubjectAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new SubjectReference(SubjectPK0, SubjectRK0));

                // Assert
                Assert.Equal(
                    new OwnerReference[] { new(OwnerPK0, OwnerRK0), new(OwnerPK0, OwnerRK0 + "-suffix"), new(OwnerPK0 + "-suffix", OwnerRK0) },
                    result);
            }

            public TheGetReferencesToSubjectAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheGetDeletedSubjectsAsyncMethod : ReferenceTrackerTest
        {
            [Fact]
            public async Task ReturnsNothingForCleanTable()
            {
                var result = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);
                Assert.Empty(result);
            }

            [Fact]
            public async Task ReturnsNothingWhenThereHaveBeenNoDeletes()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1) } }, });

                // Act
                var result = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);

                // Assert
                Assert.Empty(result);
            }

            [Fact]
            public async Task ReturnsDeleted()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } }, });

                // Act
                var result = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);

                // Assert
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK0), new(SubjectPK1, SubjectRK6) },
                    result);
            }

            [Fact]
            public async Task ObservesPagingTakeParameter()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1), SE(SubjectPK0, SubjectRK2), SE(SubjectPK0, SubjectRK3) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, ReferenceTracker.EmptySet }, });

                // Act
                var resultA = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, last: null, take: 1);
                var resultB = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, resultA.Last(), take: 1);
                var resultC = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, resultB.Last(), take: 3);

                // Assert
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK0) },
                    resultA);
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK1) },
                    resultB);
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK2), new(SubjectPK0, SubjectRK3) },
                    resultC);
            }

            [Fact]
            public async Task DoesNotReturnDeleteFromOneOwnerAndAddedToAnotherInSameOperation()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, ReferenceTracker.EmptySet }, { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });

                // Act
                var result = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);

                // Assert
                Assert.Empty(result);
                Assert.True(await TargetA.DoesSubjectHaveOwnersAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new(SubjectPK0, SubjectRK0)));
            }

            [Fact]
            public async Task ReturnsDeletedFromOneOwnerAndAddedToAnotherInDifferentOperation()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, ReferenceTracker.EmptySet }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });

                // Act
                var result = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);

                // Assert
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK0) },
                    result);
                Assert.True(await TargetA.DoesSubjectHaveOwnersAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new(SubjectPK0, SubjectRK0)));
            }

            [Fact]
            public async Task ReturnsDeletedThenAdded()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } }, });

                // Act
                var result = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);

                // Assert
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK0), new(SubjectPK0, SubjectRK1), new(SubjectPK1, SubjectRK6), new(SubjectPK1, SubjectRK7) },
                    result);
            }

            public TheGetDeletedSubjectsAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheDoesSubjectHaveOwnersAsyncMethod : ReferenceTrackerTest
        {
            [Fact]
            public async Task ReturnsFalseForEmptyTable()
            {
                var result = await TargetA.DoesSubjectHaveOwnersAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new SubjectReference(SubjectPK0, SubjectRK0));

                Assert.False(result);
            }

            [Theory]
            [InlineData(SubjectPK0, SubjectRK0, true)]
            [InlineData(SubjectPK0, SubjectRK1, true)]
            [InlineData(SubjectPK0, SubjectRK2, true)]
            [InlineData(SubjectPK1, SubjectRK6, true)]
            [InlineData(SubjectPK1, SubjectRK7, false)]
            [InlineData(SubjectPK1, SubjectRK8, true)]
            [InlineData(SubjectPK1, SubjectRK9, false)]
            public async Task ReturnsBasedOnLatestState(string pk, string rk, bool expected)
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>>() { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK6) } }, { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>>() { { OwnerRK1, ReferenceTracker.EmptySet }, { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK2) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK1, new Dictionary<string, IReadOnlySet<SubjectEdge>>() { { OwnerRK6, new HashSet<SubjectEdge> { SE(SubjectPK1, SubjectRK8) } }, });

                // Act
                var acutal = await TargetA.DoesSubjectHaveOwnersAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new SubjectReference(pk, rk));

                // Assert
                Assert.Equal(expected, acutal);
            }

            public TheDoesSubjectHaveOwnersAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheClearDeletedSubjectsAsyncMethod : ReferenceTrackerTest
        {
            [Fact]
            public async Task FailsWithSingleNonExistentRecord()
            {
                var ex = await Assert.ThrowsAsync<RequestFailedException>(() => TargetA.ClearDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName,
                    OwnerType,
                    SubjectType,
                    new[] { new SubjectReference(SubjectPK0, SubjectRK0) }));
                Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)ex.Status);
            }

            [Fact]
            public async Task FailsWithMultipleNonExistentRecords()
            {
                var ex = await Assert.ThrowsAsync<TableTransactionFailedException>(() => TargetA.ClearDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName,
                    OwnerType,
                    SubjectType,
                    new[] { new SubjectReference(SubjectPK0, SubjectRK0), new SubjectReference(SubjectPK0, SubjectRK1) }));
                Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)ex.Status);
            }

            [Fact]
            public async Task DeletesOnlyProvided()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK7) } }, });

                // Act
                await TargetA.ClearDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, new[] { new SubjectReference(SubjectPK0, SubjectRK0) });

                // Assert
                var remaining = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK1, SubjectRK6) },
                    remaining);
            }

            [Fact]
            public async Task IntegrationTest()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK6) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK2), SE(SubjectPK1, SubjectRK7) } }, });
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1), SE(SubjectPK1, SubjectRK8) } }, });

                // Act
                var candidates = await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType);
                var orphans = new List<SubjectReference>();
                foreach (var candidate in candidates)
                {
                    if (!await TargetA.DoesSubjectHaveOwnersAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, candidate))
                    {
                        orphans.Add(candidate);
                    }
                }
                await TargetA.ClearDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType, candidates);

                // Assert
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK1), new(SubjectPK0, SubjectRK2), new(SubjectPK1, SubjectRK6), new(SubjectPK1, SubjectRK7) },
                    candidates);
                Assert.Equal(
                    new SubjectReference[] { new(SubjectPK0, SubjectRK2), new(SubjectPK1, SubjectRK6), new(SubjectPK1, SubjectRK7) },
                    orphans);
                Assert.Empty(await TargetA.GetDeletedSubjectsAsync(_fixture.SubjectToOwnerTableName, OwnerType, SubjectType));
            }

            public TheClearDeletedSubjectsAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        public class TheAddReferencesAsyncMethod : ReferenceTrackerTest
        {
            [Fact]
            public async Task CanDeleteAllReferences()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1) } }, { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK2) } }, { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } }, { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK3), SE(SubjectPK1, SubjectRK7) } }, });
                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, ReferenceTracker.EmptySet },
                    { OwnerRK1, ReferenceTracker.EmptySet },
                    { OwnerRK2, ReferenceTracker.EmptySet },
                    { OwnerRK3, ReferenceTracker.EmptySet },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
            }

            [Fact]
            public async Task ReplacesReferences()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1) } }, { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK2) } }, { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } }, { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK3), SE(SubjectPK1, SubjectRK7) } }, });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK4), SE(SubjectPK0, SubjectRK5) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK3), SE(SubjectPK1, SubjectRK7), SE(SubjectPK1, SubjectRK8) } },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
            }

            [Fact]
            public async Task AddsReferences()
            {
                // Arrange
                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK1) } },
                    { OwnerRK1, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK0, SubjectRK2) } },
                    { OwnerRK2, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0), SE(SubjectPK1, SubjectRK6) } },
                    { OwnerRK3, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK3), SE(SubjectPK1, SubjectRK7) } },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
            }

            [Fact]
            public async Task DoesNotWriteDataWhenSettingNewEmptyReferences()
            {
                // Arrange
                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, ReferenceTracker.EmptySet },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                Assert.All(ServiceClientFactoryA.HandlerFactory.Responses, x => Assert.Equal(HttpMethod.Get, x.RequestMessage.Method));
            }

            [Fact]
            public async Task CanUpdateJustData()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } },
                };
                ServiceClientFactoryA.HandlerFactory.Clear();

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                Assert.Single(ServiceClientFactoryA.HandlerFactory.Responses, x => x.RequestMessage.Method != HttpMethod.Get);
            }

            [Fact]
            public async Task SkipsNoChangeInData()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0, new HashSet<SubjectEdge> { new(SubjectPK0, SubjectRK0, D(1)) } }, });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0, new HashSet<SubjectEdge> { new(SubjectPK0, SubjectRK0, D(1)) } },
                };
                ServiceClientFactoryA.HandlerFactory.Clear();

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
                Assert.All(ServiceClientFactoryA.HandlerFactory.Responses, x => Assert.Equal(HttpMethod.Get, x.RequestMessage.Method));
            }

            [Fact]
            public async Task CanUpdateRowKeyPrefixes()
            {
                // Arrange
                await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, new Dictionary<string, IReadOnlySet<SubjectEdge>> { { OwnerRK0 + "a", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, { OwnerRK0 + "b-0", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, { OwnerRK0 + "b", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, { OwnerRK0 + "c", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } }, });

                var references = new Dictionary<string, IReadOnlySet<SubjectEdge>>
                {
                    { OwnerRK0 + "a", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } },
                    { OwnerRK0 + "b-0", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } },
                    { OwnerRK0 + "b", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } },
                    { OwnerRK0 + "c", new HashSet<SubjectEdge> { SE(SubjectPK0, SubjectRK0) } },
                };

                // Act
                await SetReferencesAsync(references);

                // Assert
                await AssertExpectedAsync(references);
            }

            public TheAddReferencesAsyncMethod(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
            {
            }
        }

        private async Task SetReferencesAsync(IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> references)
        {
            await TargetA.SetReferencesAsync(_fixture.OwnerToSubjectTableName, _fixture.SubjectToOwnerTableName, OwnerType, SubjectType, OwnerPK0, references);
        }

        private async Task AssertExpectedAsync(IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> references)
        {
            await AssertExpectedAsync(OwnerType, SubjectType, OwnerPK0, references);
        }

        private async Task AssertExpectedAsync(
            string ownerType,
            string subjectType,
            string ownerPartitionKey,
            IReadOnlyDictionary<string, IReadOnlySet<SubjectEdge>> ownerRowKeyToSubjects)
        {
            (var ownerToSubject, var subjectToOwner) = await _fixture.GetTablesAsync(Output.GetLoggerFactory());

            var expectedOwnerToSubject = ownerRowKeyToSubjects
                .Select(pair => (
                    PartitionKey: $"{ownerType}${ownerPartitionKey}${subjectType}",
                    RowKey: pair.Key,
                    Data: new OwnerToSubjectEdges
                    {
                        Committed = pair.Value.OrderBy(x => x.PartitionKey, StringComparer.Ordinal).ThenBy(x => x.RowKey, StringComparer.Ordinal).ToList(),
                        ToAdd = Array.Empty<SubjectReference>(),
                        ToDelete = Array.Empty<SubjectReference>(),
                    }
                ))
                .Where(x => x.Data.Committed.Any())
                .OrderBy(x => x.PartitionKey, StringComparer.Ordinal)
                // These are wide entities so all row keys are suffixes with "~", meaning the natural order is different
                // when the set contains a row key that is a prefix of another row key in the set.
                .ThenBy(x => x.RowKey + WideEntitySegment.RowKeySeparator, StringComparer.Ordinal)
                .ToList();
            var actualOwnerToSubjectEntities = ownerToSubject
                .QueryAsync<WideEntitySegment>(x =>
                    x.PartitionKey.CompareTo(PartitionKeyPrefix) >= 0
                    && x.PartitionKey.CompareTo(PartitionKeyPrefix + char.MaxValue) < 0);
            var actualOwnerToSubject = await WideEntityService
                .DeserializeEntitiesAsync(
                    actualOwnerToSubjectEntities,
                    includeData: true)
                .Select(x => (
                    x.PartitionKey,
                    x.RowKey,
                    Data: MessagePackSerializer.Deserialize<OwnerToSubjectEdges>(
                        x.GetStream(),
                        NuGetInsightsMessagePack.Options)
                ))
                .ToListAsync();

            Assert.Equal(expectedOwnerToSubject, actualOwnerToSubject);

            var expectedSubjectToOwner = ownerRowKeyToSubjects
                .SelectMany(x => x.Value.Select(y => new { OwnerRowKey = x.Key, Subject = y }))
                .Select(x => (
                    PartitionKey: $"{subjectType}${x.Subject.PartitionKey}${x.Subject.RowKey}${ownerType}${ownerPartitionKey}",
                    RowKey: x.OwnerRowKey
                ))
                .OrderBy(x => x.PartitionKey, StringComparer.Ordinal)
                .ThenBy(x => x.RowKey, StringComparer.Ordinal)
                .ToList();
            var actualSubjectToOwnerEntities = await subjectToOwner
                .QueryAsync<TableEntity>(x =>
                    x.PartitionKey.CompareTo(PartitionKeyPrefix) >= 0
                    && x.PartitionKey.CompareTo(PartitionKeyPrefix + char.MaxValue) < 0)
                .ToListAsync();
            var actualSubjectToOwner = actualSubjectToOwnerEntities
                .Where(x => x.PartitionKey.StartsWith(SubjectType + ReferenceTracker.Separator, StringComparison.Ordinal))
                .Select(x => (x.PartitionKey, x.RowKey))
                .ToList();

            if (!expectedSubjectToOwner.SequenceEqual(actualSubjectToOwner))
            {
                Output.WriteLine("Expected subject-to-owner records:");
                OutputKeys(expectedSubjectToOwner);
                Output.WriteLine(string.Empty);

                Output.WriteLine("Actual subject-to-owner records:");
                OutputKeys(actualSubjectToOwner);
                Output.WriteLine(string.Empty);
            }

            Assert.Equal(expectedSubjectToOwner, actualSubjectToOwner);
        }

        private int _nextByte;

        private SubjectEdge SE(string pk, string rk)
        {
            return new(pk, rk, D());
        }

        private byte[] D(int? number = null)
        {
            if (number == null)
            {
                number = Interlocked.Increment(ref _nextByte);
            }

            return Encoding.UTF8.GetBytes(number.Value.ToString(CultureInfo.InvariantCulture));
        }

        private void OutputKeys(List<(string PartitionKey, string RowKey)> keys)
        {
            foreach (var (pk, rk) in keys)
            {
                Output.WriteLine($"    ($\"{{{pk.Replace("$", "}${", StringComparison.Ordinal)}}}\", {rk}),");
            }
        }

        private readonly Fixture _fixture;

        public ReferenceTrackerTest(Fixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            Output = output;
            PartitionKeyPrefix = StorageUtility.GenerateDescendingId().Unique + "-";
            OwnerType = PartitionKeyPrefix + OwnerTypeSuffix;
            SubjectType = PartitionKeyPrefix + SubjectTypeSuffix;

            ServiceClientFactoryA = new TestServiceClientFactory(
                () => new LoggingHandler(output.GetLogger<LoggingHandler>()),
                _fixture.HttpClientHandler,
                _fixture.Options.Object,
                Output.GetLoggerFactory());

            ServiceClientFactoryB = new TestServiceClientFactory(
                () => new LoggingHandler(output.GetLogger<LoggingHandler>()),
                _fixture.HttpClientHandler,
                _fixture.Options.Object,
                Output.GetLoggerFactory());

            WideEntityServiceA = new WideEntityService(
                ServiceClientFactoryA,
                output.GetTelemetryClient(),
                _fixture.Options.Object);

            WideEntityServiceB = new WideEntityService(
                ServiceClientFactoryB,
                output.GetTelemetryClient(),
                _fixture.Options.Object);

            TargetA = new ReferenceTracker(
                WideEntityServiceA,
                ServiceClientFactoryA,
                _fixture.Options.Object);

            TargetB = new ReferenceTracker(
                WideEntityServiceB,
                ServiceClientFactoryB,
                _fixture.Options.Object);
        }

        public ITestOutputHelper Output { get; }
        public string PartitionKeyPrefix { get; }
        public string OwnerType { get; }
        public string SubjectType { get; }
        public TestServiceClientFactory ServiceClientFactoryA { get; }
        public TestServiceClientFactory ServiceClientFactoryB { get; }
        public WideEntityService WideEntityServiceA { get; }
        public WideEntityService WideEntityServiceB { get; }
        public ReferenceTracker TargetA { get; }
        public ReferenceTracker TargetB { get; }

        public class Fixture : IAsyncLifetime
        {
            private bool _created;

            public Fixture()
            {
                Options = new Mock<IOptions<NuGetInsightsSettings>>();
                Settings = new NuGetInsightsSettings
                {
                    StorageConnectionString = TestSettings.StorageConnectionString,
                };
                var prefix = TestSettings.NewStoragePrefix();
                OwnerToSubjectTableName = prefix + "1o2s1";
                SubjectToOwnerTableName = prefix + "1s2o1";
                Options.Setup(x => x.Value).Returns(() => Settings);

                HttpClientHandler = new HttpClientHandler { AllowAutoRedirect = false };
            }

            public Mock<IOptions<NuGetInsightsSettings>> Options { get; }
            public NuGetInsightsSettings Settings { get; }
            public string OwnerToSubjectTableName { get; }
            public string SubjectToOwnerTableName { get; }
            public HttpClientHandler HttpClientHandler { get; }

            public async Task InitializeAsync()
            {
                await GetTablesAsync(NullLoggerFactory.Instance);
            }

            public ServiceClientFactory GetServiceClientFactory(ILoggerFactory loggerFactory)
            {
                return new ServiceClientFactory(Options.Object, loggerFactory);
            }

            public async Task DisposeAsync()
            {
                (var ownerToSubject, var subjectToOwner) = await GetTablesAsync(NullLoggerFactory.Instance);
                await ownerToSubject.DeleteAsync();
                await subjectToOwner.DeleteAsync();
            }

            public async Task<(TableClientWithRetryContext OwnerToSubject, TableClientWithRetryContext SubjectToOwner)> GetTablesAsync(ILoggerFactory loggerFactory)
            {
                var ownerToSubject = (await GetServiceClientFactory(loggerFactory).GetTableServiceClientAsync())
                    .GetTableClient(OwnerToSubjectTableName);
                var subjectToOwner = (await GetServiceClientFactory(loggerFactory).GetTableServiceClientAsync())
                    .GetTableClient(SubjectToOwnerTableName);

                if (!_created)
                {
                    await ownerToSubject.CreateIfNotExistsAsync();
                    await subjectToOwner.CreateIfNotExistsAsync();
                    _created = true;
                }

                return (ownerToSubject, subjectToOwner);
            }
        }
    }
}
