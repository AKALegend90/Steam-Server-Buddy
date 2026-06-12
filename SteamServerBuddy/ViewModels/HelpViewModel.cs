using CommunityToolkit.Mvvm.ComponentModel;

namespace SteamServerBuddy.ViewModels
{
    public partial class HelpViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _helpText;

        public HelpViewModel()
        {
            LoadInstructions();
        }

        private void LoadInstructions()
        {
            HelpText =
@"Steam Server Buddy Tutorial

Install a server
1. Open Server Catalog.
2. Search for the dedicated server name or AppID.
3. Click Install.
4. Review the install folder and SteamCMD command.
5. Click Install and wait for SteamCMD to finish.

Add a server manually
1. Open Install Server.
2. Enter the Steam dedicated server AppID.
3. Click Lookup so the app fills the name, art, links, and command.
4. Choose or edit the install folder.
5. Click Install.

Start and manage a server
1. Open Dashboard or Installed Servers.
2. Click Details on the server.
3. Use Start, Stop, Restart, Update / Validate, and Open Folder from the details page.
4. Use Server Settings Files to edit detected config values.
5. Use Backups before large config changes.

Manual port forwarding
1. Open the server Details page.
2. Check the Port field. If it shows 0, click Detect Port or enter the port from the game's server documentation.
3. Open Windows Defender Firewall.
4. Add inbound rules for the server port. Most Steam dedicated servers need UDP. Some also need TCP.
5. Open your router admin page in a browser. Common addresses are 192.168.1.1, 192.168.0.1, or the Default Gateway shown by ipconfig.
6. Find Port Forwarding, NAT, Virtual Server, or Gaming.
7. Add a rule that forwards the server port to this PC's local IPv4 address.
8. Use the same protocol the game requires, usually UDP. If unsure, add both UDP and TCP.
9. Save the router settings and restart the server.

Finding this PC's local IPv4 address
1. Open Command Prompt.
2. Run ipconfig.
3. Look for IPv4 Address under your active Ethernet or Wi-Fi adapter.
4. Use that address in the router port-forward rule.

When port forwarding still does not work
1. Make sure the server is running before testing.
2. Make sure your router forwards to the correct local IPv4 address.
3. Make sure Windows Firewall allows the port.
4. Check whether your ISP uses CGNAT. If your router WAN IP is different from the public IP shown by a website, port forwarding may not work from home.
5. If CGNAT blocks you, use a tunnel/VPN option like playit, Tailscale, or Meshnet, or rent a hosted server.";
        }
    }
}
