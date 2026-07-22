using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using SteamServerBuddy.Models;

namespace SteamServerBuddy.Services
{
    public class AutomationService : IDisposable
    {
        private readonly System.Timers.Timer _timer;
        private readonly ConcurrentDictionary<string, DateTime> _lastBackups = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastUpdates = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastRestarts = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastHealthChecks = new();
        private readonly ConcurrentDictionary<string, int> _healthFailures = new();
        private readonly ConcurrentDictionary<string, byte> _startupHandled = new();
        private readonly ConcurrentDictionary<string, byte> _restartInProgress = new();
        private readonly ConcurrentDictionary<string, byte> _sentWarnings = new();
        private int _isRunning;

        public AutomationService()
        {
            _timer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
            _timer.Elapsed += async (_, _) => await TickAsync();
        }

        public void Start()
        {
            Globals.Diagnostics.Info("Automation service started.");
            _timer.Start();
            _ = TickAsync();
        }

        public async Task TickAsync()
        {
            if (Interlocked.Exchange(ref _isRunning, 1) == 1) return;

            try
            {
                var servers = await Globals.WebAPI.FetchDedicatedServersAsync();
                foreach (var server in servers.Where(server => server.IsInstalled))
                {
                    await HandleStartupAsync(server);
                    await RunBackupIfDueAsync(server);
                    await RunUpdateIfDueAsync(server);
                    await RunRestartIfDueAsync(server);
                    await RunHealthCheckIfDueAsync(server);
                }
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error("Automation tick failed", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private async Task HandleStartupAsync(ServerInfo server)
        {
            if (!_startupHandled.TryAdd(server.AppId, 0)) return;

            var now = DateTime.Now;
            _lastBackups.TryAdd(server.AppId, now);
            _lastUpdates.TryAdd(server.AppId, now);

            await Task.CompletedTask;
        }

        private async Task RunBackupIfDueAsync(ServerInfo server)
        {
            if (!server.AutoBackupEnabled || string.IsNullOrWhiteSpace(server.InstallPath)) return;

            var intervalMinutes = server.AutoBackupIntervalMinutes > 0
                ? server.AutoBackupIntervalMinutes
                : Math.Max(1, server.AutoBackupIntervalHours) * 60;
            var now = DateTime.Now;
            var last = _lastBackups.GetOrAdd(server.AppId, now);
            if (now - last < TimeSpan.FromMinutes(intervalMinutes)) return;

            await CreateBackupAsync(server, "Scheduled backup completed.");
        }

        private async Task CreateBackupAsync(ServerInfo server, string message)
        {
            try
            {
                await Globals.Backups.CreateBackupAsync(server.DisplayName, server.InstallPath, server.BackupLocation);
                await Globals.Backups.PruneBackupsOlderThanAsync(
                    server.InstallPath,
                    Math.Max(1, server.BackupRetentionDays),
                    server.BackupLocation);
                _lastBackups[server.AppId] = DateTime.Now;
                await Globals.ProcessManager.NotifyDiscord(server.AppId, message, "#4299E1", NotificationType.Info);
                Globals.Diagnostics.Info($"{message} Server: {server.DisplayName}.");
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Backup failed for {server.DisplayName}", ex);
            }
        }

        private async Task RunUpdateIfDueAsync(ServerInfo server)
        {
            if (!server.AutoUpdateEnabled || server.PinServerVersion || Globals.ProcessManager.IsRunning(server.AppId)) return;

            var interval = TimeSpan.FromMinutes(Math.Max(5, server.AutoUpdateCheckIntervalMinutes));
            var last = _lastUpdates.GetOrAdd(server.AppId, DateTime.Now);
            if (DateTime.Now - last < interval) return;

            await UpdateServerAsync(server, "Automatic update");
        }

        private async Task<bool> UpdateServerAsync(ServerInfo server, string operation)
        {
            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await Globals.SteamCMD.InstallServerAsync(
                    server.AppId,
                    server.InstallPath,
                    msg => Globals.Diagnostics.Info($"{operation} {server.DisplayName}: {msg}"),
                    success => tcs.TrySetResult(success));

                var success = await tcs.Task;
                _lastUpdates[server.AppId] = DateTime.Now;
                if (success)
                {
                    await Globals.ProcessManager.NotifyDiscord(server.AppId, $"{operation} completed.", "#4299E1", NotificationType.Update);
                }

                return success;
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"{operation} failed for {server.DisplayName}", ex);
                return false;
            }
        }

        private async Task RunRestartIfDueAsync(ServerInfo server)
        {
            if (!server.ScheduledRestartEnabled || !Globals.ProcessManager.IsRunning(server.AppId)) return;
            if (!TryGetRestartTime(server, out var restartAt)) return;

            var now = DateTime.Now;
            var uptime = Globals.ProcessManager.GetUptime(server.AppId);
            if (uptime < TimeSpan.FromHours(Math.Max(0, server.RestartMinimumUptimeHours))) return;

            if (server.AnnounceRestarts && restartAt > now)
            {
                var minutesRemaining = (int)Math.Ceiling((restartAt - now).TotalMinutes);
                var announcementMinutes = ParseAnnouncementMinutes(server.RestartAnnouncementMinutes);
                if (announcementMinutes.Contains(minutesRemaining))
                {
                    var warningKey = $"{server.AppId}:{restartAt:yyyyMMdd}:{minutesRemaining}";
                    if (_sentWarnings.TryAdd(warningKey, 0))
                    {
                        var message = $"Scheduled restart in {minutesRemaining} minute{(minutesRemaining == 1 ? "" : "s")}.";
                        Globals.Diagnostics.Info($"{server.DisplayName}: {message}");
                        await Globals.ProcessManager.NotifyDiscord(server.AppId, message, "#FFC107", NotificationType.Info);
                    }
                }
            }

            if (now < restartAt || now - restartAt > TimeSpan.FromMinutes(10)) return;
            if (_lastRestarts.TryGetValue(server.AppId, out var last) && last.Date == now.Date) return;
            if (!_restartInProgress.TryAdd(server.AppId, 0)) return;

            _lastRestarts[server.AppId] = now;
            _ = RestartServerAsync(server);
        }

        private async Task RestartServerAsync(ServerInfo server)
        {
            try
            {
                Globals.Diagnostics.Info($"Scheduled restart started for {server.DisplayName}.");
                Globals.ProcessManager.StopServer(server.AppId);
                await Task.Delay(5000);

                if (server.BackupOnShutdown)
                {
                    await CreateBackupAsync(server, "Pre-restart backup completed.");
                }

                if ((server.ValidateOnRestart || server.UpdateOnStartRestart) && !server.PinServerVersion)
                {
                    await UpdateServerAsync(server, server.ValidateOnRestart ? "Restart validation" : "Restart update");
                }

                var exe = Globals.Executables.FindServerExecutable(server.InstallPath);
                if (string.IsNullOrWhiteSpace(exe)) return;
                if (!await Globals.DirectX.EnsureLegacyRuntimeAsync(null, allowPrompt: false)) return;

                if (server.BackupOnStartup)
                {
                    await CreateBackupAsync(server, "Pre-start backup completed.");
                }

                Globals.ProcessManager.StartServer(server.AppId, exe, server.LaunchArguments ?? "");
                Globals.Diagnostics.Info($"Scheduled restart completed for {server.DisplayName}.");
            }
            finally
            {
                _restartInProgress.TryRemove(server.AppId, out _);
            }
        }

        private async Task RunHealthCheckIfDueAsync(ServerInfo server)
        {
            if (!server.HealthCheckEnabled || !Globals.ProcessManager.IsRunning(server.AppId)) return;

            var now = DateTime.Now;
            var interval = TimeSpan.FromSeconds(Math.Max(5, server.HealthCheckIntervalSeconds));
            var last = _lastHealthChecks.GetOrAdd(server.AppId, DateTime.MinValue);
            if (now - last < interval) return;
            _lastHealthChecks[server.AppId] = now;

            if (Globals.ProcessManager.IsResponsive(server.AppId))
            {
                _healthFailures[server.AppId] = 0;
                return;
            }

            var failures = _healthFailures.AddOrUpdate(server.AppId, 1, (_, value) => value + 1);
            if (failures < Math.Max(1, server.HealthCheckFailureThreshold)) return;

            _healthFailures[server.AppId] = 0;
            await Globals.ProcessManager.NotifyDiscord(server.AppId, "Health check failed repeatedly; restarting server.", "#FFC107", NotificationType.Crash);
            if (_restartInProgress.TryAdd(server.AppId, 0))
            {
                _ = RestartServerAsync(server);
            }
        }

        private static bool TryGetRestartTime(ServerInfo server, out DateTime restartAt)
        {
            restartAt = default;
            if (!DateTime.TryParse(server.ScheduledRestartTime, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
            {
                return false;
            }

            restartAt = DateTime.Today.Add(parsed.TimeOfDay);
            return true;
        }

        private static int[] ParseAnnouncementMinutes(string value)
        {
            return (value ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(part, out var minute) ? minute : -1)
                .Where(minute => minute > 0)
                .Distinct()
                .ToArray();
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
