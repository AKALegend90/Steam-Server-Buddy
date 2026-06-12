using System.Diagnostics;
using System.Threading.Tasks;

namespace SteamServerBuddy.Services
{
    public class FirewallService
    {
        private static FirewallService _instance;
        public static FirewallService Instance => _instance ??= new FirewallService();

        public async Task<bool> AllowPortAsync(int port, string ruleName, bool isUdp = true)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        // TODO: Implement Linux firewall (ufw/iptables)
                        return true; 
                    }

                    string protocol = isUdp ? "UDP" : "TCP";
                    // Check if rule exists first to avoid duplicates? 
                    // netsh advfirewall firewall show rule name="ruleName"
                    // For simplicity, we can delete and re-add, or just add. 
                    // Adding specific rule:
                    
                    var psi = new ProcessStartInfo("netsh", $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol={protocol} localport={port} profile=any")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false, // Must be false to redirect output, but true for RunAs... 
                        // If we need RunAs, we need UseShellExecute=true and cannot redirect output easily.
                        // Let's try running it. If the main app is not admin, this will fail.
                        Verb = "runas" 
                    };

                    // If we use UseShellExecute = true, we can trigger the UAC prompt for JUST this command!
                    psi.UseShellExecute = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;

                    var process = Process.Start(psi);
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
