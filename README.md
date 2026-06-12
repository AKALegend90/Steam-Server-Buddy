# Steam Server Buddy

**Steam Server Buddy** is a Windows desktop app for installing, launching, and managing Steam dedicated servers with SteamCMD.

This repository is now on **V2**. The latest release is **v2.0.1**.

## Download

1. Open the [Releases page](../../releases).
2. Download the latest `SteamServerBuddy-v2.0.1-win-x64.zip`.
3. Extract the zip.
4. Run `SteamServerBuddy.exe`.

On first use, Steam Server Buddy can set up SteamCMD for you from inside the app.

## V2 Features

- **SteamCMD setup and repair**: Download, check, and fix SteamCMD from the sidebar.
- **Install dedicated servers**: Add a server by Steam AppID or choose one from the built-in Server Catalog.
- **Built-in Server Catalog**: Search a local list of known Steam dedicated server AppIDs without opening SteamDB.
- **Steam artwork and metadata**: Shows server names, Steam artwork, SteamDB links, Steam Store links, and install commands when available.
- **Installed server library**: View installed/imported servers, open folders, remove entries, and jump into details.
- **Server controls**: Start, stop, restart, update, validate, and open server folders.
- **Live console area**: Shows app/server log output when a readable log stream is available.
- **Configuration editor**: Detects supported server setting files and lets you edit common config values in the app.
- **Raw config editing**: Open and edit raw config files when the app cannot map every setting.
- **Port detection helper**: Tries to detect server ports from config files.
- **Manual port forwarding tutorial**: Includes an in-app guide for router port forwarding, Windows Firewall, local IPv4 address, and CGNAT checks.
- **Backups**: Create and manage backups for server files.
- **Automation options**: Auto restart after crash, auto update, scheduled restart, and auto backup.
- **Import existing servers**: Add already-installed server folders to the app.
- **Themes**: Switch between dark and light themes.
- **Discord notifications**: Optional webhook notifications for server events.

## Server Catalog

The catalog is an offline built-in list of Steam dedicated server tools. It does not scrape SteamDB live.

This makes the app more reliable because SteamDB does not provide a simple public API for searching every dedicated server from inside the app. If a server is missing, it can still be added manually with its Steam AppID.

Examples included in the catalog:

- Palworld Dedicated Server - `2394010`
- Enshrouded Dedicated Server - `2278520`
- RuneScape Dragonwilds: Dedicated Server - `4019830`
- Satisfactory Dedicated Server - `1690800`
- Valheim Dedicated Server - `896660`
- V Rising Dedicated Server - `1829350`
- Rust Dedicated Server - `258550`
- 7 Days to Die Dedicated Server - `294420`

## Basic Use

1. Open **Server Catalog** or **Install Server**.
2. Select a dedicated server or enter a Steam AppID.
3. Click **Lookup** if using an AppID manually.
4. Choose the install folder.
5. Click **Install**.
6. Open **Installed Servers**.
7. Click **Details** to start, stop, update, validate, edit configs, or create backups.

## Port Forwarding

Automatic router port forwarding was removed in V2 because router UPnP/NAT-PMP support is inconsistent and often disabled.

Use the **Tutorial** tab in the app for manual setup:

- Find the server port in the Details page.
- Add a Windows Firewall inbound rule for the server port.
- Open your router admin page.
- Forward the server port to this PC's local IPv4 address.
- Use UDP unless the game's documentation says TCP is also needed.
- If port forwarding still fails, check whether your ISP uses CGNAT.

## Notes

- Player count display was removed because many dedicated servers do not answer the same query protocol reliably.
- Some Steam dedicated server AppIDs do not have their own artwork. V2 uses known game artwork aliases where possible.
- The app stores its data under `%APPDATA%\SteamServerBuddy`.

## Feedback

Please report bugs or feature requests in the [Issues tab](../../issues).

Created by [AKALegend90](https://github.com/AKALegend90).
