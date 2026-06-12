# Steam Server Buddy - User Guide

## Getting Started

1.  **Extract the App**: Unzip the release file to a folder.
2.  **Run**: Double-click `SteamServerBuddy.exe`.
3.  **First Run**: The app will automatically download and initialize SteamCMD.

## Features

### managing Servers
- **Add Server**: Use the specific tab to add supported game servers.
- **Install/Update**: detailed view allows one-click installation and updates.
- **Control**: Start and Stop servers easily. Monitor CPU and RAM usage.

### Automation
- **Auto-Restart**: Automatically brings the server back up if it crashes.
- **Auto-Update**: Schedule updates to run automatically.
- **Backups**: Configure automatic backups to keep your data safe.

### Network & Port Forwarding
Steam Server Buddy now includes **Auto-Port Forwarding (UPnP)**!
1. Go to the **Settings** tab (Gear icon) for your server.
2. Scroll to the **Automation** section.
3. Enable **"Auto-Port Forward (UPnP)"**.
4. When you click **START**, the app will attempt to automatically open the required ports on your router.

*Note: This requires your router to have UPnP enabled. If it fails (or if your ISP uses CGNAT), you will still need to manually forward ports.*

### Discord Notifications
- Configure a Webhook URL in settings to receive alerts for server starts, stops, and crashes.

#### How to Setup Discord Webhook
1.  **Open Discord**: Right-click the text channel where you want alerts.
2.  **Edit Channel**: Select "Edit Channel" from the menu.
3.  **Integrations**: Go to the "Integrations" tab and click "Webhooks".
4.  **Create**: Click "New Webhook".
5.  **Copy**: Click "Copy Webhook URL".
6.  **Paste**: Paste this URL into the Server Buddy settings.

## Support
Created by [AKALegend90](https://github.com/AKALegend90)
