using System;
using System.IO;
using Newtonsoft.Json;

namespace SteamServerBuddy.Services
{
    public class AppSettings
    {
        public string ServerInstallPath { get; set; } = "";
        public string DiscordWebhookUrl { get; set; } = "";
        public bool EnableDiscordAlerts { get; set; }
    }

    public class AppSettingsService
    {
        private readonly string _settingsPath;
        private AppSettings _settings;

        public AppSettingsService()
        {
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.json");
            Load();
        }

        private void Load()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    _settings = new AppSettings();
                }
            }
            else
            {
                _settings = new AppSettings();
            }

            // Default install path if not set
            if (string.IsNullOrEmpty(_settings.ServerInstallPath))
            {
                _settings.ServerInstallPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "servers");
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch
            {
                // Silently fail
            }
        }

        public string GetServerInstallPath() => _settings.ServerInstallPath;

        public void SetServerInstallPath(string path)
        {
            _settings.ServerInstallPath = path;
            Save();
        }

        public string GetDiscordWebhookUrl() => _settings.DiscordWebhookUrl;

        public void SetDiscordWebhookUrl(string url)
        {
            _settings.DiscordWebhookUrl = url;
            Save();
        }

        public bool GetEnableDiscordAlerts() => _settings.EnableDiscordAlerts;

        public void SetEnableDiscordAlerts(bool enabled)
        {
            _settings.EnableDiscordAlerts = enabled;
            Save();
        }
    }
}
