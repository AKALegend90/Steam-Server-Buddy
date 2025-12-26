using System;
using Newtonsoft.Json;

namespace SteamServerBuddy.Models
{
    public class ServerInfo
    {
        [JsonProperty("appid")]
        public string AppId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("install_path")]
        public string InstallPath { get; set; }

        [JsonProperty("is_installed")]
        public bool IsInstalled { get; set; }

        [JsonIgnore]
        public bool IsRunning { get; set; }
        
        [JsonIgnore]
        public string DisplayName => string.IsNullOrEmpty(Name) ? $"AppID: {AppId}" : Name;

        [JsonProperty("auto_restart")]
        public bool AutoRestart { get; set; }

        [JsonProperty("discord_webhook")]
        public string DiscordWebhookUrl { get; set; }

        [JsonProperty("enable_discord")]
        public bool EnableDiscordAlerts { get; set; }

        [JsonProperty("notify_on_start")]
        public bool NotifyOnStart { get; set; } = true;

        [JsonProperty("notify_on_stop")]
        public bool NotifyOnStop { get; set; } = true;

        [JsonProperty("notify_on_crash")]
        public bool NotifyOnCrash { get; set; } = true;

        [JsonProperty("notify_on_update")]
        public bool NotifyOnUpdate { get; set; } = true;

        // Auto Backup
        [JsonProperty("auto_backup_enabled")]
        public bool AutoBackupEnabled { get; set; }

        [JsonProperty("auto_backup_interval_hours")]
        public int AutoBackupIntervalHours { get; set; } = 24; // Default daily

        // Auto Update
        [JsonProperty("auto_update_enabled")]
        public bool AutoUpdateEnabled { get; set; }
        
        [JsonProperty("auto_update_schedule")]
        public string AutoUpdateSchedule { get; set; } = "04:00 AM"; // Default 4 AM

        [JsonProperty("auto_update_day")]
        public string AutoUpdateDay { get; set; } = "Daily"; // Daily, Monday, etc.
    }
}
