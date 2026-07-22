using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SteamServerBuddy.Models;

namespace SteamServerBuddy.Services
{
    public enum NotificationType
    {
        Start,
        Stop,
        Crash,
        Update,
        Info
    }

    public class PerformanceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public double MemoryUsageMb { get; set; }
    }

    public class ProcessManager
    {
        private readonly ConcurrentDictionary<string, Process> _runningServers = new();
        private readonly ConcurrentDictionary<string, (TimeSpan TotalProcessorTime, DateTime Time)> _cpuTracking = new();
        private readonly ConcurrentDictionary<string, bool> _intentionallyStoppedServers = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCrashRestart = new();
        private readonly ConcurrentDictionary<string, int> _crashRestartCounts = new();

        public void StartServer(string appId, string exePath, string args = "")
        {
            if (_runningServers.TryGetValue(appId, out var existing))
            {
                if (!existing.HasExited) return;
                _runningServers.TryRemove(appId, out _);
            }

            var workingDir = Path.GetDirectoryName(exePath);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args ?? "",
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            try
            {
                var proc = Process.Start(psi);
                if (proc == null) return;

                proc.EnableRaisingEvents = true;
                proc.Exited += async (_, _) => await HandleServerExit(appId);
                _runningServers.TryAdd(appId, proc);
                _cpuTracking.TryAdd(appId, (proc.TotalProcessorTime, DateTime.UtcNow));

                Globals.Diagnostics.Info($"Server started: {appId} ({exePath})");
                _ = NotifyDiscord(appId, "Server started.", "#4CAF50", NotificationType.Start);
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Failed to start server {appId}", ex);
                throw;
            }
        }

        public void StopServer(string appId)
        {
            _intentionallyStoppedServers.TryAdd(appId, true);

            if (!_runningServers.TryRemove(appId, out var proc)) return;

            try
            {
                proc.EnableRaisingEvents = false;

                if (!proc.HasExited)
                {
                    proc.CloseMainWindow();
                    if (!proc.WaitForExit(5000))
                    {
                        proc.Kill(true);
                        proc.WaitForExit(1000);
                    }
                }

                Globals.Diagnostics.Info($"Server stopped: {appId}");
                _ = NotifyDiscord(appId, "Server stopped manually.", "#F44336", NotificationType.Stop);
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Failed to stop server {appId}", ex);
            }
        }

        public bool IsRunning(string appId)
        {
            return _runningServers.TryGetValue(appId, out var proc) && !proc.HasExited;
        }

        public PerformanceMetrics GetPerformance(string appId)
        {
            if (!_runningServers.TryGetValue(appId, out var proc) || proc.HasExited)
            {
                return new PerformanceMetrics();
            }

            try
            {
                proc.Refresh();

                var cpu = 0.0;
                var mem = proc.WorkingSet64 / 1024.0 / 1024.0;

                if (_cpuTracking.TryGetValue(appId, out var last))
                {
                    var now = DateTime.UtcNow;
                    var currentCpu = proc.TotalProcessorTime;
                    var cpuUsedMs = (currentCpu - last.TotalProcessorTime).TotalMilliseconds;
                    var totalTimeMs = (now - last.Time).TotalMilliseconds;

                    if (totalTimeMs > 0)
                    {
                        cpu = (cpuUsedMs / totalTimeMs) / Environment.ProcessorCount * 100;
                    }

                    _cpuTracking[appId] = (currentCpu, now);
                }
                else
                {
                    _cpuTracking[appId] = (proc.TotalProcessorTime, DateTime.UtcNow);
                }

                return new PerformanceMetrics
                {
                    CpuUsagePercent = Math.Round(cpu, 1),
                    MemoryUsageMb = Math.Round(mem, 1)
                };
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Failed to read performance for {appId}", ex);
                return new PerformanceMetrics();
            }
        }

        public Process? GetProcess(string appId)
        {
            return _runningServers.TryGetValue(appId, out var proc) && !proc.HasExited ? proc : null;
        }

        public bool IsResponsive(string appId)
        {
            var process = GetProcess(appId);
            if (process == null) return false;

            try
            {
                process.Refresh();
                return process.Responding;
            }
            catch
            {
                return false;
            }
        }

        public TimeSpan GetUptime(string appId)
        {
            var process = GetProcess(appId);
            if (process == null) return TimeSpan.Zero;

            try
            {
                return DateTime.Now - process.StartTime;
            }
            catch
            {
                return TimeSpan.Zero;
            }
        }

        private async Task HandleServerExit(string appId)
        {
            _runningServers.TryRemove(appId, out _);
            _cpuTracking.TryRemove(appId, out _);

            if (_intentionallyStoppedServers.TryRemove(appId, out _))
            {
                return;
            }

            var server = await GetServerInfoSafe(appId);
            await NotifyDiscord(appId, "Server process exited unexpectedly.", "#FFC107", NotificationType.Crash);

            if (server?.BackupOnShutdown == true && !string.IsNullOrWhiteSpace(server.InstallPath))
            {
                try
                {
                    await Globals.Backups.CreateBackupAsync(server.DisplayName, server.InstallPath, server.BackupLocation);
                    await Globals.Backups.PruneBackupsOlderThanAsync(server.InstallPath, Math.Max(1, server.BackupRetentionDays), server.BackupLocation);
                }
                catch (Exception ex)
                {
                    Globals.Diagnostics.Error($"Crash backup failed for {server.DisplayName}", ex);
                }
            }

            if (server == null || !server.AutoRestart || string.IsNullOrEmpty(server.InstallPath)) return;

            if (IsCrashLooping(appId))
            {
                await NotifyDiscord(appId, "Auto-restart paused after repeated crashes.", "#F44336", NotificationType.Crash);
                Globals.Diagnostics.Warn($"Auto-restart paused for {server.DisplayName}; crash loop protection triggered.");
                return;
            }

            try
            {
                await Task.Delay(5000);
                var exePath = Globals.Executables.FindServerExecutable(server.InstallPath);

                if (!string.IsNullOrEmpty(exePath))
                {
                    if (server.BackupOnStartup)
                    {
                        await Globals.Backups.CreateBackupAsync(server.DisplayName, server.InstallPath, server.BackupLocation);
                    }
                    await NotifyDiscord(appId, "Server auto-restarting.", "#2196F3", NotificationType.Start);
                    StartServer(appId, exePath, server.LaunchArguments ?? "");
                }
                else
                {
                    await NotifyDiscord(appId, "Auto-restart failed: could not find executable.", "#F44336", NotificationType.Crash);
                }
            }
            catch (Exception ex)
            {
                await NotifyDiscord(appId, $"Auto-restart error: {ex.Message}", "#F44336", NotificationType.Crash);
            }
        }

        private bool IsCrashLooping(string appId)
        {
            var now = DateTime.Now;
            var count = 1;

            if (_lastCrashRestart.TryGetValue(appId, out var last) && now - last < TimeSpan.FromMinutes(10))
            {
                count = _crashRestartCounts.AddOrUpdate(appId, 1, (_, existing) => existing + 1);
            }
            else
            {
                _crashRestartCounts[appId] = 1;
            }

            _lastCrashRestart[appId] = now;
            return count > 3;
        }

        public async Task NotifyDiscord(string appId, string msg, string color, NotificationType type = NotificationType.Info)
        {
            var server = await GetServerInfoSafe(appId);

            if (!Globals.AppSettings.GetEnableDiscordAlerts()) return;

            if (server != null)
            {
                switch (type)
                {
                    case NotificationType.Start when !server.NotifyOnStart:
                    case NotificationType.Stop when !server.NotifyOnStop:
                    case NotificationType.Crash when !server.NotifyOnCrash:
                    case NotificationType.Update when !server.NotifyOnUpdate:
                        return;
                }
            }

            var webhookUrl = !string.IsNullOrWhiteSpace(server?.DiscordWebhookUrl)
                ? server.DiscordWebhookUrl
                : Globals.AppSettings.GetDiscordWebhookUrl();

            if (string.IsNullOrWhiteSpace(webhookUrl)) return;

            var serverName = server?.DisplayName ?? appId;
            await Globals.Notification.SendDiscordAlertAsync(webhookUrl, $"[{serverName}] {msg}", color);
        }

        private async Task<ServerInfo?> GetServerInfoSafe(string appId)
        {
            try
            {
                var list = await Globals.WebAPI.FetchDedicatedServersAsync();
                foreach (var s in list)
                {
                    if (s.AppId == appId) return s;
                }
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Failed to fetch server info for {appId}", ex);
            }

            return null;
        }
    }
}
