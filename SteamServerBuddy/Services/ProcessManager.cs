using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SteamServerBuddy.Models;
using SteamServerBuddy;

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
        private readonly ConcurrentDictionary<string, Process> _runningServers = new ConcurrentDictionary<string, Process>();
        
        // Tracking previous CPU checks for calculation
        private readonly ConcurrentDictionary<string, (TimeSpan TotalProcessorTime, DateTime Time)> _cpuTracking = new();

        // Track servers that were intentionally stopped (to prevent auto-restart)
        private readonly ConcurrentDictionary<string, bool> _intentionallyStoppedServers = new();

        public void StartServer(string appId, string exePath, string args = "")
        {
            if (_runningServers.ContainsKey(appId))
            {
                var existing = _runningServers[appId];
                if (!existing.HasExited) return; // Already running
                _runningServers.TryRemove(appId, out _);
            }

            var workingDir = Path.GetDirectoryName(exePath);
            
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = true
            };

            try 
            {
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += async (s, e) => await HandleServerExit(appId);
                    _runningServers.TryAdd(appId, proc);
                    
                    // Initialize CPU tracking
                    _cpuTracking.TryAdd(appId, (proc.TotalProcessorTime, DateTime.UtcNow));

                    // Send Discord Alert
                    _ = NotifyDiscord(appId, $"🟢 Server Started", "#4CAF50", NotificationType.Start); // Green
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start server {appId}: {ex.Message}");
                throw;
            }
        }

        public void StopServer(string appId)
        {
            // Mark as intentionally stopped BEFORE removing from running servers
            _intentionallyStoppedServers.TryAdd(appId, true);

            if (_runningServers.TryRemove(appId, out var proc))
            {
                try
                {
                    // Disable raising events so we don't trigger auto-restart on manual stop
                    proc.EnableRaisingEvents = false;

                    if (!proc.HasExited)
                    {
                        proc.Kill(true); // Force kill
                        proc.WaitForExit(1000);
                    }
                    
                    // Send Discord Alert
                    _ = NotifyDiscord(appId, $"🔴 Server Stopped (Manual)", "#F44336", NotificationType.Stop); // Red
                }
                catch { }
            }
        }

        public bool IsRunning(string appId)
        {
            if (_runningServers.TryGetValue(appId, out var proc))
            {
                return !proc.HasExited;
            }
            return false;
        }

        public PerformanceMetrics GetPerformance(string appId)
        {
            if (_runningServers.TryGetValue(appId, out var proc) && !proc.HasExited)
            {
                try
                {
                    proc.Refresh(); // Important to get latest stats
                    
                    double cpu = 0;
                    double mem = proc.WorkingSet64 / 1024.0 / 1024.0; // MB

                    // Calculate CPU Usage
                    // Needs delta from last check
                    if (_cpuTracking.TryGetValue(appId, out var last))
                    {
                        var now = DateTime.UtcNow;
                        var currentCpu = proc.TotalProcessorTime;
                        
                        var cpuUsedMs = (currentCpu - last.TotalProcessorTime).TotalMilliseconds;
                        var totalTimeMs = (now - last.Time).TotalMilliseconds;
                        
                        if (totalTimeMs > 0)
                        {
                            // Divide by logical cores to get system-wide % equivalent or keep as single core %
                            // Usually task manager shows % of total CPU power.
                            // Dotnet returns usage across all cores (so 100% = 1 core fully used? No, TotalProcessorTime sums up)
                            // Formula: (UsageDelta / TimeDelta) / Cores * 100
                            
                            cpu = (cpuUsedMs / totalTimeMs) / Environment.ProcessorCount * 100;
                        }
                        
                        // Update tracking
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
                catch
                {
                    return new PerformanceMetrics();
                }
            }
            return new PerformanceMetrics();
        }

        private async Task HandleServerExit(string appId)
        {
            _runningServers.TryRemove(appId, out _);
            _cpuTracking.TryRemove(appId, out _);
            
            // Check if this was an intentional stop - if so, don't auto-restart
            if (_intentionallyStoppedServers.TryRemove(appId, out _))
            {
                // This was a manual stop, don't auto-restart or send crash alert
                return;
            }

            // Check if we need to auto-restart
            var server = await GetServerInfoSafe(appId);
             
             // Send Discord Alert (Crash)
             await NotifyDiscord(appId, $"⚠️ Server Process Exited Unexpectedly", "#FFC107", NotificationType.Crash); // Orange

            if (server != null && server.AutoRestart && !string.IsNullOrEmpty(server.InstallPath))
            {
                // Auto-Restart Logic
                try
                {
                    // Wait a bit to prevent rapid loop
                    await Task.Delay(5000);

                    // We need to know the executable to restart it.
                    // Since StartServer args are not stored, we try to heuristically find it or use a stored property if we had it.
                    // Ideally, we should update ServerInfo to store the "LastExecutablePath" or similar.
                    // For now, let's look for the standard executable using the same logic as ViewModel.

                    var exePath = FindServerExecutable(server.InstallPath);
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        await NotifyDiscord(appId, "🔄 Server Auto-Restarting...", "#2196F3", NotificationType.Start); // Blue
                        StartServer(appId, exePath);
                    }
                    else
                    {
                        await NotifyDiscord(appId, "❌ Auto-Restart Failed: Could not find executable.", "#F44336", NotificationType.Crash);
                    }
                }
                catch (Exception ex)
                {
                    await NotifyDiscord(appId, $"❌ Auto-Restart Error: {ex.Message}", "#F44336", NotificationType.Crash);
                }
            }
        }
        
        public async Task NotifyDiscord(string appId, string msg, string color, NotificationType type = NotificationType.Info)
        {
             var server = await GetServerInfoSafe(appId);
             
             // Check global toggle from app settings (per-server toggle is deprecated/hidden)
             bool enabled = Globals.AppSettings.GetEnableDiscordAlerts();
             if (!enabled) return;

             // Check granular toggles if server info is available
             if (server != null)
             {
                 switch (type)
                 {
                     case NotificationType.Start: if (!server.NotifyOnStart) return; break;
                     case NotificationType.Stop: if (!server.NotifyOnStop) return; break;
                     case NotificationType.Crash: if (!server.NotifyOnCrash) return; break;
                     case NotificationType.Update: if (!server.NotifyOnUpdate) return; break;
                 }
             }

             // Determine URL: Server specific > Global App Settings
             var webhookUrl = !string.IsNullOrWhiteSpace(server?.DiscordWebhookUrl) 
                 ? server.DiscordWebhookUrl 
                 : Globals.AppSettings.GetDiscordWebhookUrl();

             if (!string.IsNullOrWhiteSpace(webhookUrl))
             {
                 var serverName = server?.DisplayName ?? appId;
                 var finalMsg = $"[{serverName}] {msg}";
                 await Globals.Notification.SendDiscordAlertAsync(webhookUrl, finalMsg, color);
             }
        }
        
        // Helper to get server info without locking
        private async Task<ServerInfo> GetServerInfoSafe(string appId)
        {
            // We need to fetch from the full list.
            var list = await Globals.WebAPI.FetchDedicatedServersAsync();
            foreach (var s in list) if (s.AppId == appId) return s;
            return null;
        }

        // Duplicated logic from ViewModel, should be in a shared helper but putting here for now to avoid refactor complexity
        private string FindServerExecutable(string installPath)
        {
            if (!Directory.Exists(installPath)) return null;
            // 1. Look for obvious "start.bat" or "run.bat"
            var batFiles = Directory.GetFiles(installPath, "*start*.bat").Concat(Directory.GetFiles(installPath, "*run*.bat"));
            var bat = batFiles.FirstOrDefault();
            if (bat != null) return bat;

            // 2. Look for obvious exe with "server" in name
            var exeFiles = Directory.GetFiles(installPath, "*.exe");
            var serverExe = exeFiles.FirstOrDefault(f => f.ToLower().Contains("server") && !f.ToLower().Contains("unity"));
            if (serverExe != null) return serverExe;

            // 3. Fallback
            return exeFiles.FirstOrDefault();
        }
    }
}
