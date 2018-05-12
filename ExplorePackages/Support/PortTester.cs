using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Support
{
    public class PortTester : IPortTester
    {
        private readonly ILogger<PortTester> _logger;

        public PortTester(ILogger<PortTester> logger)
        {
            _logger = logger;
        }

        public async Task<bool> IsPortOpenAsync(string host, int port, TimeSpan connectTimeout)
        {
            var isPortOpen = await IsPortOpenInternalAsync(host, port, connectTimeout);
            _logger.LogInformation("Port {Port} on {Host} is {PortStatus}.", port, host, isPortOpen ? "open" : "closed");
            return isPortOpen;
        }

        private async Task<bool> IsPortOpenInternalAsync(string host, int port, TimeSpan connectTimeout)
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

                return true;
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
    }
}
