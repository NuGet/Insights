using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using NuGet.Common;

namespace Knapcode.ExplorePackages.Support
{
    public class PortTester : IPortTester
    {
        private readonly ILogger _log;

        public PortTester(ILogger log)
        {
            _log = log;
        }

        public async Task<bool> IsPortOpenAsync(string host, int port, bool requireSsl, TimeSpan connectTimeout)
        {
            var isPortOpen = await IsPortOpenInternalAsync(host, port, requireSsl, connectTimeout);
            _log.LogInformation($"Port {port} on {host} is {(isPortOpen ? "open" : "closed")}.");
            return isPortOpen;
        }

        private async Task<bool> IsPortOpenInternalAsync(string host, int port, bool requireSsl, TimeSpan connectTimeout)
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
    }
}
