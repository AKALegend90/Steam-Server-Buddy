using System;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Linq;

namespace SteamServerBuddy.Services
{
    public class NetworkService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> GetPublicIpAsync()
        {
            try
            {
                // Simple Public IP check using minimal API
                var response = await _httpClient.GetStringAsync("https://api.ipify.org");
                return response.Trim();
            }
            catch (Exception)
            {
                return "Unavailable";
            }
        }

        public async Task<bool> IsPortListeningAsync(int port)
        {
            if (port <= 0 || port > 65535) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // Check active TCP listeners
                    var ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                    var tcpListeners = ipProperties.GetActiveTcpListeners();
                    if (tcpListeners.Any(e => e.Port == port)) return true;

                    // CHECK UDP listeners (many game servers use UDP)
                    var udpListeners = ipProperties.GetActiveUdpListeners();
                    if (udpListeners.Any(e => e.Port == port)) return true;

                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
