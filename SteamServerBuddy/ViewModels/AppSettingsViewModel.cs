using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

        public AppSettingsViewModel()
        {
            ServerInstallPath = Globals.AppSettings.GetServerInstallPath();
            DiscordWebhookUrl = Globals.AppSettings.GetDiscordWebhookUrl();
            EnableDiscordAlerts = Globals.AppSettings.GetEnableDiscordAlerts();
        }

        [RelayCommand]
        public void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                ServerInstallPath = dialog.FolderName;
                Globals.AppSettings.SetServerInstallPath(ServerInstallPath);
            }
        }

        [RelayCommand]
        public void SaveWebhook()
        {
            Globals.AppSettings.SetDiscordWebhookUrl(DiscordWebhookUrl);
            Globals.AppSettings.SetEnableDiscordAlerts(EnableDiscordAlerts);
        }

        [RelayCommand]
        public async System.Threading.Tasks.Task TestDiscord()
        {
            if (string.IsNullOrEmpty(DiscordWebhookUrl)) return;
            Globals.AppSettings.SetDiscordWebhookUrl(DiscordWebhookUrl); // Save before test
            await Globals.Notification.SendDiscordAlertAsync(DiscordWebhookUrl, "👋 Hello from Steam Server Buddy! Your webhook is working.", "#4299E1"); // Blue
        }
    }
}
