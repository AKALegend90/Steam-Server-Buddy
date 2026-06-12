using System;
using System.Collections.Concurrent;
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
        private int _isRunning;

        public AutomationService()
        {
            _timer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
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
                foreach (var server in servers)
                {
                    await RunBackupIfDueAsync(server);
                    await RunUpdateIfDueAsync(server);
                    RunRestartIfDue(server);
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

        private async Task RunBackupIfDueAsync(ServerInfo server)
        {
            if (!server.AutoBackupEnabled || server.AutoBackupIntervalHours <= 0 || string.IsNullOrWhiteSpace(server.InstallPath)) return;

            var now = DateTime.Now;
            if (_lastBackups.TryGetValue(server.AppId, out var last) &&
                now - last < TimeSpan.FromHours(server.AutoBackupIntervalHours))
            {
                return;
            }

            try
            {
                await Globals.Backups.CreateBackupAsync(server.DisplayName, server.InstallPath);
                await Globals.Backups.PruneBackupsAsync(server.InstallPath, 10);
                _lastBackups[server.AppId] = now;
                await Globals.ProcessManager.NotifyDiscord(server.AppId, "Automatic backup completed.", "#4299E1", NotificationType.Info);
                Globals.Diagnostics.Info($"Automatic backup completed for {server.DisplayName}.");
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Automatic backup failed for {server.DisplayName}", ex);
            }
        }

        private async Task RunUpdateIfDueAsync(ServerInfo server)
        {
            if (!server.AutoUpdateEnabled || string.IsNullOrWhiteSpace(server.AutoUpdateSchedule)) return;
            if (!IsScheduledNow(server.AutoUpdateSchedule, server.AutoUpdateDay)) return;

            var todayKey = DateTime.Today;
            if (_lastUpdates.TryGetValue(server.AppId, out var last) && last.Date == todayKey) return;

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                await Globals.SteamCMD.InstallServerAsync(
                    server.AppId,
                    server.InstallPath,
                    msg => Globals.Diagnostics.Info($"Auto-update {server.DisplayName}: {msg}"),
                    success => tcs.TrySetResult(success));

                if (await tcs.Task)
                {
                    _lastUpdates[server.AppId] = DateTime.Now;
                    await Globals.ProcessManager.NotifyDiscord(server.AppId, "Automatic update completed.", "#4299E1", NotificationType.Update);
                }
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Automatic update failed for {server.DisplayName}", ex);
            }
        }

        private void RunRestartIfDue(ServerInfo server)
        {
            if (!server.ScheduledRestartEnabled || server.ScheduledRestartIntervalHours <= 0) return;
            if (!Globals.ProcessManager.IsRunning(server.AppId)) return;

            var now = DateTime.Now;
            if (!_lastRestarts.TryGetValue(server.AppId, out var last))
            {
                _lastRestarts[server.AppId] = now;
                return;
            }

            if (now - last < TimeSpan.FromHours(server.ScheduledRestartIntervalHours)) return;

            var exe = Globals.Executables.FindServerExecutable(server.InstallPath);
            if (string.IsNullOrWhiteSpace(exe)) return;

            _lastRestarts[server.AppId] = now;
            Globals.ProcessManager.StopServer(server.AppId);
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                Globals.ProcessManager.StartServer(server.AppId, exe, server.LaunchArguments ?? "");
            });
        }

        private static bool IsScheduledNow(string schedule, string day)
        {
            if (!string.IsNullOrWhiteSpace(day) &&
                !day.Equals("Daily", StringComparison.OrdinalIgnoreCase) &&
                !day.Equals(DateTime.Now.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!DateTime.TryParse(schedule, out var target)) return false;
            var now = DateTime.Now;
            return now.Hour == target.Hour && now.Minute == target.Minute;
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
