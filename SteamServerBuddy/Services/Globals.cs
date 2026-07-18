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
        private static readonly Lazy<DiagnosticsService> _diagnostics = new Lazy<DiagnosticsService>(() => new DiagnosticsService());
        private static readonly Lazy<ServerExecutableService> _executables = new Lazy<ServerExecutableService>(() => new ServerExecutableService());
        private static readonly Lazy<DialogService> _dialogs = new Lazy<DialogService>(() => new DialogService());
        private static readonly Lazy<AutomationService> _automation = new Lazy<AutomationService>(() => new AutomationService());
        private static readonly Lazy<AppThemeService> _theme = new Lazy<AppThemeService>(() => new AppThemeService());
        private static readonly Lazy<DirectXRuntimeService> _directX = new Lazy<DirectXRuntimeService>(() => new DirectXRuntimeService());

        public static SteamWebAPIService WebAPI => _webAPI.Value;
        public static SteamCMDService SteamCMD => _steamCMD.Value;
        public static ProcessManager ProcessManager => _processManager.Value;
        public static ConfigService Config => _config.Value;
        public static AppSettingsService AppSettings => _appSettings.Value;
        public static NotificationService Notification => _notification.Value;
        public static DiagnosticsService Diagnostics => _diagnostics.Value;
        public static ServerExecutableService Executables => _executables.Value;
        public static DialogService Dialogs => _dialogs.Value;
        public static AutomationService Automation => _automation.Value;
        public static AppThemeService Theme => _theme.Value;
        public static DirectXRuntimeService DirectX => _directX.Value;
        private static readonly Lazy<BackupService> _backups = new Lazy<BackupService>(() => new BackupService());
        public static BackupService Backups => _backups.Value;

        private static readonly Lazy<NetworkService> _network = new Lazy<NetworkService>(() => NetworkService.Instance);

        public static NetworkService Network => _network.Value;

        private static readonly Lazy<FirewallService> _firewall = new Lazy<FirewallService>(() => FirewallService.Instance);
        public static FirewallService Firewall => _firewall.Value;



    }
}
