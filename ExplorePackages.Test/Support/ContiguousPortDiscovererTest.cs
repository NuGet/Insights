using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using Xunit;

namespace Knapcode.ExplorePackages.Support
{
    public class ContiguousPortDiscovererTest
    {
        public class IsPortOpenAsync
        {
            [Theory]
            [MemberData(nameof(TestData))]
            public async Task ReturnsExpectedPorts(int startingPort, int[] available, int[] expected)
            {
                // Arrange
                var host = "example";
                var requireSsl = false;
                var connectTimeout = TimeSpan.FromSeconds(10);

                var portTester = new Mock<IPortTester>();
                foreach (var port in available)
                {
                    portTester
                        .Setup(x => x.IsPortOpenAsync(
                            host,
                            port,
                            requireSsl,
                            connectTimeout))
                        .ReturnsAsync(true);
                }
                
                var target = new ContiguousPortDiscoverer(portTester.Object);

                // Act
                var actual = await target.FindPortsAsync(
                    host,
                    startingPort,
                    requireSsl,
                    connectTimeout);

                // Assert
                Assert.Equal(expected, actual);
            }

            public static IEnumerable<object[]> TestData => InternalTestData
                .Select(x => new object[] { x.StartingPort, x.AvailablePorts, x.ExpectedPorts })
                .ToList();

            private static IEnumerable<TestCase> InternalTestData => new[]
            {
                new TestCase(new int[0]),
                new TestCase(new int[] { 0 }),
                new TestCase(new int[] { 0, 1 }),
                new TestCase(new int[] { 0, 1, 2 }),
                new TestCase(new int[] { 0, 1, 2, 3 }),
                new TestCase(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9}),
                new TestCase(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }),
                new TestCase(0, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9}),
                new TestCase(1, new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9}),
                new TestCase(ushort.MaxValue, new int[] { 0 }, new int[] { 0 }),
                new TestCase(ushort.MaxValue - 1, new int[] { 0, 1, 2, 3 }, new int[] { 0, 1 }),
                new TestCase(ushort.MaxValue - 3, new int[] { 0, 1, 2, 3, 4, 5 }, new int[] { 0, 1, 2, 3 }),
                new TestCase(new int[] { 0, 1, 2, 4 }, new int[] { 0, 1, 2 }),
                new TestCase(new int[] { 0, 1, 2, 5 }, new int[] { 0, 1, 2 }),
                new TestCase(new int[] { 0, 1, 2, 6 }, new int[] { 0, 1, 2 }),
                new TestCase(new int[] { 0, 1, 2, 7 }, new int[] { 0, 1, 2 }),
                new TestCase(new int[] { 0, 1, 2, 8 }, new int[] { 0, 1, 2 }),
                new TestCase(new int[] { 0, 1, 2, 9 }, new int[] { 0, 1, 2 }),
            };

            private class TestCase
            {
                private const int DefaultPort = 10000;


                public TestCase(int[] available)
                    : this(DefaultPort, available, available)
                {
                }
                public TestCase(ushort startingPort, int[] available)
                    : this(startingPort, available, available)
                {
                }

                public TestCase(int[] available, int[] expected)
                    : this(DefaultPort, available, available)
                {
                }

                public TestCase(ushort startingPort, int[] available, int[] expected)
                {
                    StartingPort = startingPort;
                    AvailablePorts = available
                        .Select(x => StartingPort + x)
                        .ToArray();
                    ExpectedPorts = expected
                        .Select(x => StartingPort + x)
                        .ToArray();
                }

                public ushort StartingPort { get; }
                public int[] AvailablePorts { get; }
                public int[] ExpectedPorts { get; }
            }
        }
    }
}
