using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    public class SimplePortDiscoverer : IPortDiscoverer
    {
        private const int ParallelConnections = 6;
        private const int AllowedClosedPorts = 2;
        private readonly IPortTester _portTester;

        public SimplePortDiscoverer(IPortTester portTester)
        {
            _portTester = portTester;
        }

        public async Task<IReadOnlyList<int>> FindPortsAsync(
            string host,
            int startingPort,
            bool requireSsl,
            TimeSpan connectTimeout)
        {
            var openPorts = new List<int>();
            var currentStartingPort = startingPort;
            var closedPortsFound = 0;

            while (closedPortsFound < AllowedClosedPorts)
            {
                var tasks = Enumerable
                    .Range(currentStartingPort, ParallelConnections)
                    .Select(port => new
                    {
                        Port = port,
                        Task = _portTester.IsPortOpenAsync(
                            host,
                            port,
                            requireSsl,
                            connectTimeout)
                    })
                    .ToList();
                await Task.WhenAll(tasks.Select(x => x.Task));

                openPorts.AddRange(tasks
                    .Where(x => x.Task.Result)
                    .Select(x => x.Port));
                currentStartingPort += ParallelConnections;
                closedPortsFound += tasks.Count(x => !x.Task.Result);
            }

            return openPorts;
        }
    }
}
