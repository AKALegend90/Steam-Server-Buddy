using SteamServerBuddy.Services;
using System;

namespace SteamServerBuddy
{
    public static class Globals
    {
        private static readonly Lazy<SteamWebAPIService> _webAPI = new Lazy<SteamWebAPIService>(() => new SteamWebAPIService());
        private static readonly Lazy<SteamCMDService> _steamCMD = new Lazy<SteamCMDService>(() => new SteamCMDService());
        private static readonly Lazy<ProcessManager> _processManager = new Lazy<ProcessManager>(() => new ProcessManager());
        private static readonly Lazy<ConfigService> _config = new Lazy<ConfigService>(() => new ConfigService());
        private static readonly Lazy<AppSettingsService> _appSettings = new Lazy<AppSettingsService>(() => new AppSettingsService());
        private static readonly Lazy<NotificationService> _notification = new Lazy<NotificationService>(() => new NotificationService());

        public static SteamWebAPIService WebAPI => _webAPI.Value;
        public static SteamCMDService SteamCMD => _steamCMD.Value;
        public static ProcessManager ProcessManager => _processManager.Value;
        public static ConfigService Config => _config.Value;
        public static AppSettingsService AppSettings => _appSettings.Value;
        public static NotificationService Notification => _notification.Value;

        private static readonly Lazy<BackupService> _backups = new Lazy<BackupService>(() => new BackupService());
        public static BackupService Backups => _backups.Value;

        private static readonly Lazy<NetworkService> _network = new Lazy<NetworkService>(() => new NetworkService());
        public static NetworkService Network => _network.Value;



    }
}
