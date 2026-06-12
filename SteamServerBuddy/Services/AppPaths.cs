using System;
using System.IO;

namespace SteamServerBuddy.Services
{
    public static class AppPaths
    {
        public static string DataDir { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SteamServerBuddy");

        public static string ServersDir => Path.Combine(DataDir, "servers");
        public static string SteamCmdDir => Path.Combine(DataDir, "steamcmd");
        public static string SettingsPath => Path.Combine(DataDir, "app_settings.json");
        public static string CustomServersPath => Path.Combine(DataDir, "custom_servers.json");
        public static string ManifestsDir => Path.Combine(DataDir, "manifests");
        public static string UserPathsPath => Path.Combine(ManifestsDir, "user_paths.json");

        public static void EnsureDataDirectories()
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(ServersDir);
            Directory.CreateDirectory(ManifestsDir);
        }
    }
}
