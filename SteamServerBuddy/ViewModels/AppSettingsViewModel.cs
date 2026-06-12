using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace SteamServerBuddy.ViewModels
{
    public partial class AppSettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _serverInstallPath;

        [ObservableProperty]
        private string _discordWebhookUrl;

        [ObservableProperty]
        private bool _enableDiscordAlerts;

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private string _theme = AppThemeService.DarkTheme;

        public IReadOnlyList<string> ThemeOptions { get; } = new[]
        {
            AppThemeService.DarkTheme,
            AppThemeService.LightTheme
        };

        public AppSettingsViewModel()
        {
            ServerInstallPath = Globals.AppSettings.GetServerInstallPath();
            DiscordWebhookUrl = Globals.AppSettings.GetDiscordWebhookUrl();
            EnableDiscordAlerts = Globals.AppSettings.GetEnableDiscordAlerts();
            _theme = Globals.AppSettings.GetTheme();
        }

        partial void OnThemeChanged(string value)
        {
            var theme = Globals.Theme.Normalize(value);
            Globals.AppSettings.SetTheme(theme);
            Globals.Theme.Apply(theme);
            StatusMessage = $"{theme} theme applied.";
        }

        [RelayCommand]
        public async Task BrowseFolder()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow?.StorageProvider is not { } storageProvider)
            {
                StatusMessage = "Folder picker is unavailable.";
                return;
            }

            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose default server install folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                ServerInstallPath = folders[0].Path.LocalPath;
                Globals.AppSettings.SetServerInstallPath(ServerInstallPath);
                StatusMessage = "Server install path saved.";
            }
        }

        [RelayCommand]
        public void SaveWebhook()
        {
            Globals.AppSettings.SetServerInstallPath(ServerInstallPath);
            Globals.AppSettings.SetDiscordWebhookUrl(DiscordWebhookUrl);
            Globals.AppSettings.SetEnableDiscordAlerts(EnableDiscordAlerts);
            Globals.AppSettings.SetTheme(Theme);
            Globals.Theme.Apply(Theme);
            StatusMessage = "Settings saved.";
        }

        [RelayCommand]
        public async Task TestDiscord()
        {
            if (string.IsNullOrEmpty(DiscordWebhookUrl)) return;

            Globals.AppSettings.SetDiscordWebhookUrl(DiscordWebhookUrl);
            await Globals.Notification.SendDiscordAlertAsync(
                DiscordWebhookUrl,
                "Hello from Steam Server Buddy! Your webhook is working.",
                "#4299E1");
            StatusMessage = "Test notification sent.";
        }

        [RelayCommand]
        public void OpenDiagnosticsLog()
        {
            try
            {
                var logPath = Globals.Diagnostics.LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                if (!File.Exists(logPath)) File.WriteAllText(logPath, "");

                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true
                });
            }
            catch
            {
                StatusMessage = "Could not open diagnostics log.";
            }
        }
    }
}
