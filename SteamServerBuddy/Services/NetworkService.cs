using System;
using System.Linq;
using System.Net.NetworkInformation;

namespace SteamServerBuddy.Services
{
    public class NetworkService
    {
        private static readonly Lazy<NetworkService> _instance = new Lazy<NetworkService>(() => new NetworkService());
        public static NetworkService Instance => _instance.Value;

        private NetworkService() { }

        public bool IsUdpPortInUse(int port)
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                return properties.GetActiveUdpListeners().Any(p => p.Port == port);
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Could not check UDP port {port}", ex);
                return false;
            }
        }
    }
}
