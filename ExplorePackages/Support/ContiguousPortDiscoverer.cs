using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Knapcode.ExplorePackages.Support
{
    /// <summary>
    /// Given a starting port, finds the maximum port for range of contiguous open ports. This is great for discovering
    /// the number of instances in a Azure Cloud Service definition file that exposes instance endpoints:
    /// <see cref="https://docs.microsoft.com/en-us/azure/cloud-services/cloud-services-enable-communication-role-instances#instance-input-endpoint"/>
    /// </summary>
    public class ContiguousPortDiscoverer : IPortDiscoverer
    {
        private readonly IPortTester _portTester;

        public ContiguousPortDiscoverer(IPortTester portTester)
        {
            _portTester = portTester;
        }

        public async Task<IReadOnlyList<int>> FindPortsAsync(
            string host,
            int startingPort,
            bool requireSsl,
            TimeSpan connectTimeout)
        {
            var maximumPort = await FindMaximumPortAsync(host, startingPort, requireSsl, connectTimeout);
            if (!maximumPort.HasValue)
            {
                return new List<int>();
            }

            return Enumerable
                .Range(startingPort, (maximumPort.Value - startingPort) + 1)
                .ToList();
        }

        private async Task<int?> FindMaximumPortAsync(string host, int startingPort, bool requireSsl, TimeSpan connectTimeout)
        {
            var state = new State(host, requireSsl, connectTimeout);
            var maxPort = ushort.MaxValue + 1;
            do
            {
                maxPort = await FindMaximumPortAsync(state, startingPort, maxPort);

                if (state.HighestValidPort.HasValue)
                {
                    startingPort = state.HighestValidPort.Value;
                }
            }
            while (maxPort - startingPort > 1 && state.HighestValidPort.HasValue);

            return state.HighestValidPort;
        }

        private async Task<int> FindMaximumPortAsync(State state, int startingPort, int maxPort)
        {
            int increment = 1;
            int currentPort = startingPort;
            while (currentPort < maxPort)
            {
                bool isPortOpen;
                if (!state.ValidPorts.Contains(currentPort))
                {
                    isPortOpen = await _portTester.IsPortOpenAsync(
                        state.Host,
                        currentPort,
                        state.RequireSsl,
                        state.ConnectTimeout);

                    if (isPortOpen)
                    {
                        state.ValidPorts.Add(currentPort);
                        if (!state.HighestValidPort.HasValue
                            || currentPort > state.HighestValidPort.Value)
                        {
                            state.HighestValidPort = currentPort;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                currentPort += increment;
                increment *= 2;
            }

            return Math.Min(currentPort, maxPort);
        }

        private class State
        {
            public State(string host, bool requireSsl, TimeSpan connectTimeout)
            {
                Host = host;
                RequireSsl = requireSsl;
                ConnectTimeout = connectTimeout;
                ValidPorts = new HashSet<int>();
                HighestValidPort = null;
            }

            public string Host { get; }
            public bool RequireSsl { get; }
            public TimeSpan ConnectTimeout { get; }
            public ISet<int> ValidPorts { get; }
            public int? HighestValidPort { get; set; }
        }
    }
}
