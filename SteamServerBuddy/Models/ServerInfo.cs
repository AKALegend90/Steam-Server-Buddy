using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SteamServerBuddy.Models
{
    public class ServerInfo
    {
        [JsonProperty("appid")]
        public string AppId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("steam_type")]
        public string SteamType { get; set; } = "";

        [JsonProperty("header_image_url")]
        public string HeaderImageUrl { get; set; } = "";

        [JsonProperty("capsule_image_url")]
        public string CapsuleImageUrl { get; set; } = "";

        [JsonProperty("steamdb_url")]
        public string SteamDbUrl { get; set; } = "";

        [JsonProperty("steam_store_url")]
        public string SteamStoreUrl { get; set; } = "";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("install_path")]
        public string InstallPath { get; set; }

        [JsonProperty("launch_arguments")]
        public string LaunchArguments { get; set; } = "";

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

        [JsonProperty("auto_backup_interval_minutes")]
        public int AutoBackupIntervalMinutes { get; set; } = 1440;

        [JsonProperty("backup_on_startup")]
        public bool BackupOnStartup { get; set; }

        [JsonProperty("backup_on_shutdown")]
        public bool BackupOnShutdown { get; set; }

        [JsonProperty("backup_retention_days")]
        public int BackupRetentionDays { get; set; } = 7;

        [JsonProperty("backup_location")]
        public string BackupLocation { get; set; } = "";

        // Auto Update
        [JsonProperty("auto_update_enabled")]
        public bool AutoUpdateEnabled { get; set; }
        
        [JsonProperty("auto_update_schedule")]
        public string AutoUpdateSchedule { get; set; } = "04:00 AM"; // Default 4 AM

        [JsonProperty("port")]
        public int Port { get; set; } = 0; // 0 means use default logic

        [JsonProperty("auto_update_day")]
        public string AutoUpdateDay { get; set; } = "Daily"; // Daily, Monday, etc.

        [JsonProperty("pin_server_version")]
        public bool PinServerVersion { get; set; }

        [JsonProperty("update_on_start_restart")]
        public bool UpdateOnStartRestart { get; set; }

        [JsonProperty("auto_update_check_interval_minutes")]
        public int AutoUpdateCheckIntervalMinutes { get; set; } = 15;

        // Scheduled Restart
        [JsonProperty("scheduled_restart_enabled")]
        public bool ScheduledRestartEnabled { get; set; }

        [JsonProperty("scheduled_restart_interval_hours")]
        public int ScheduledRestartIntervalHours { get; set; } = 6; // Default 6 hours

        [JsonProperty("scheduled_restart_time")]
        public string ScheduledRestartTime { get; set; } = "03:00 AM";

        [JsonProperty("restart_minimum_uptime_hours")]
        public int RestartMinimumUptimeHours { get; set; } = 2;

        [JsonProperty("announce_restarts")]
        public bool AnnounceRestarts { get; set; } = true;

        [JsonProperty("restart_announcement_minutes")]
        public string RestartAnnouncementMinutes { get; set; } = "15,5,1";

        [JsonProperty("validate_on_restart")]
        public bool ValidateOnRestart { get; set; }

        [JsonProperty("health_check_enabled")]
        public bool HealthCheckEnabled { get; set; }

        [JsonProperty("health_check_failure_threshold")]
        public int HealthCheckFailureThreshold { get; set; } = 10;

        [JsonProperty("health_check_interval_seconds")]
        public int HealthCheckIntervalSeconds { get; set; } = 7;
    }
}
