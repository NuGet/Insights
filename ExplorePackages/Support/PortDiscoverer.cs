using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Support
{
    /// <summary>
    /// Given a starting port, finds the maximum port for range of contiguous open ports. This is great for discovering
    /// the number of instances in a Azure Cloud Service definition file that exposes instance endpoints:
    /// <see cref="https://docs.microsoft.com/en-us/azure/cloud-services/cloud-services-enable-communication-role-instances#instance-input-endpoint"/>
    /// </summary>
    public class PortDiscoverer
    {
        private readonly ILogger _log;

        public PortDiscoverer(ILogger log)
        {
            _log = log;
        }

        public async Task<int?> FindMaximumPortAsync(string host, int startingPort, bool requireSsl, TimeSpan connectTimeout)
        {
            var state = new State(host, requireSsl, connectTimeout);
            var maxPort = ushort.MaxValue + 1;
            while (maxPort - startingPort > 1)
            {
                maxPort = await FindMaximumPortAsync(state, startingPort, maxPort);
                startingPort = state.HighestValidPort.Value;
            }

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
                    isPortOpen = await IsPortOpenAsync(state.Host, currentPort, state.RequireSsl, state.ConnectTimeout);
                    _log.LogInformation($"Port {currentPort} on {state.Host} is {(isPortOpen ? "open" : "closed")}.");

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

        private async Task<bool> IsPortOpenAsync(string host, int port, bool requireSsl, TimeSpan connectTimeout)
        {
            using (var tcpClient = new TcpClient())
            {
                var connectTask = tcpClient.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(connectTimeout);

                var firstTask = await Task.WhenAny(connectTask, timeoutTask);
                if (firstTask == timeoutTask)
                {
                    return false;
                }

                if (requireSsl)
                {
                    try
                    {
                        using (var networkStream = tcpClient.GetStream())
                        using (var sslStream = new SslStream(
                            networkStream,
                            leaveInnerStreamOpen: false,
                            userCertificateValidationCallback: AcceptAllCertificates))
                        {
                            await sslStream.AuthenticateAsClientAsync(host);
                            return true;
                        }
                    }
                    catch (IOException)
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
        }

        public static bool AcceptAllCertificates(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
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
