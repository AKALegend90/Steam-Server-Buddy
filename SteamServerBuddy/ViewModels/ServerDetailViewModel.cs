using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SteamServerBuddy.ViewModels
{
    public partial class ServerDetailViewModel : ObservableObject
    {
        public ConsoleViewModel ConsoleVM { get; } = new ConsoleViewModel();
        public SettingsViewModel SettingsVM { get; } = new SettingsViewModel();

        // maintenance
        public System.Collections.ObjectModel.ObservableCollection<Services.BackupInfo> Backups { get; } = new();




        [ObservableProperty]
        private string _serverName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPalworld))]
        [NotifyPropertyChangedFor(nameof(ShowDashboardContent))]
        [NotifyPropertyChangedFor(nameof(ShowSettingsContent))]
        private string _appId;

        public bool IsPalworld => AppId == "2394010";
        public bool ShowDashboardContent => !IsSettingsMode;
        public bool ShowSettingsContent => IsSettingsMode;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowDashboardContent))]
        [NotifyPropertyChangedFor(nameof(ShowSettingsContent))]
        private bool _isSettingsMode;

        [ObservableProperty]
        private string _installPath;

        [ObservableProperty]
        private string _detailStatus = "";

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private string _installStatus = "";

        [ObservableProperty]
        private bool _isInstalling = false;

        // Automation & Monitoring
        [ObservableProperty]
        private bool _autoRestart;

        [ObservableProperty]
        private bool _autoUpdateEnabled;

        [ObservableProperty]
        private string _autoUpdateSchedule = "04:00 AM"; // HH:mm tt

        [ObservableProperty]
        private string _autoUpdateDay = "Daily";

        public System.Collections.Generic.List<string> UpdateDays { get; } = new() 
        { 
            "Daily", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" 
        };

        [ObservableProperty]
        private bool _autoBackupEnabled;

        [ObservableProperty]
        private int _autoBackupInterval = 24; 

        [ObservableProperty]
        private string _backupSchedule = "24h";

        public System.Collections.Generic.List<string> BackupSchedules { get; } = new()
        {
            "15m", "30m", "45m", "1h", "2h", "3h", "4h", "6h", "8h", "12h", "24h"
        };

        [ObservableProperty]
        private bool _backupOnStartup;

        [ObservableProperty]
        private bool _backupOnShutdown;

        [ObservableProperty]
        private int _backupRetentionDays = 7;

        [ObservableProperty]
        private string _backupLocation = "";

        [ObservableProperty]
        private string _cpuUsage = "0%";

        [ObservableProperty]
        private string _memoryUsage = "0 MB";

        [ObservableProperty]
        private int _serverPort;

        [ObservableProperty]
        private string _launchArguments = "";

        [ObservableProperty]
        private string _tunnelStatus = "Use this when router forwarding is annoying or your ISP blocks inbound traffic.";

        // Notification Toggles



        [ObservableProperty]
        private bool _scheduledRestartEnabled;

        [ObservableProperty]
        private int _scheduledRestartInterval = 6;

        [ObservableProperty]
        private string _scheduledRestartTime = "03:00 AM";

        [ObservableProperty]
        private int _restartMinimumUptimeHours = 2;

        [ObservableProperty]
        private bool _announceRestarts = true;

        [ObservableProperty]
        private string _restartAnnouncementMinutes = "15,5,1";

        [ObservableProperty]
        private bool _validateOnRestart;

        [ObservableProperty]
        private bool _pinServerVersion;

        [ObservableProperty]
        private bool _updateOnStartRestart;

        [ObservableProperty]
        private int _autoUpdateCheckIntervalMinutes = 15;

        [ObservableProperty]
        private bool _healthCheckEnabled;

        [ObservableProperty]
        private int _healthCheckFailureThreshold = 10;

        [ObservableProperty]
        private int _healthCheckIntervalSeconds = 7;

        [ObservableProperty]
        private string _generalLog = "";

        [ObservableProperty]
        private string _steamCmdLog = "SteamCMD is ready. Use Update / Validate to check this server.\n";

        [ObservableProperty]
        private string _serverLogPath = "No server log detected yet.";

        [ObservableProperty]
        private string _chatStatus = "Chat is unavailable until this game exposes a supported RCON or REST interface.";

        [ObservableProperty]
        private string _playersStatus = "Player data is unavailable until this game exposes a supported query, RCON, or REST interface.";

        [ObservableProperty]
        private string _uptimeDisplay = "-";

        [ObservableProperty]
        private string _nextRestartDisplay = "off";

        private System.Timers.Timer _monitorTimer;
        private ServerItemViewModel _currentServer;
        private Process _playitProcess;

        public ServerDetailViewModel()
        {
            _monitorTimer = new System.Timers.Timer(2000); // 2 seconds update
            _monitorTimer.Elapsed += (s, e) => UpdateStats();
        }

        public async Task LoadAsync(ServerItemViewModel server)
        {
            ServerName = server.Name;
            AppId = server.AppId;
            InstallPath = server.Info.InstallPath;
            _currentServer = server;
            IsInstalled = server.Info.IsInstalled;

            // Load automation settings
            AutoRestart = server.Info.AutoRestart;
            AutoUpdateEnabled = server.Info.AutoUpdateEnabled;
            AutoUpdateSchedule = !string.IsNullOrEmpty(server.Info.AutoUpdateSchedule) ? server.Info.AutoUpdateSchedule : "04:00 AM";
            AutoUpdateDay = !string.IsNullOrEmpty(server.Info.AutoUpdateDay) ? server.Info.AutoUpdateDay : "Daily";
            AutoBackupEnabled = server.Info.AutoBackupEnabled;
            AutoBackupInterval = server.Info.AutoBackupIntervalHours;
            BackupSchedule = ScheduleFromMinutes(server.Info.AutoBackupIntervalMinutes > 0
                ? server.Info.AutoBackupIntervalMinutes
                : Math.Max(1, server.Info.AutoBackupIntervalHours) * 60);
            BackupOnStartup = server.Info.BackupOnStartup;
            BackupOnShutdown = server.Info.BackupOnShutdown;
            BackupRetentionDays = Math.Max(1, server.Info.BackupRetentionDays);
            BackupLocation = server.Info.BackupLocation ?? "";
            ScheduledRestartEnabled = server.Info.ScheduledRestartEnabled;
            ScheduledRestartInterval = server.Info.ScheduledRestartIntervalHours;
            ScheduledRestartTime = string.IsNullOrWhiteSpace(server.Info.ScheduledRestartTime) ? "03:00 AM" : server.Info.ScheduledRestartTime;
            RestartMinimumUptimeHours = Math.Max(0, server.Info.RestartMinimumUptimeHours);
            AnnounceRestarts = server.Info.AnnounceRestarts;
            RestartAnnouncementMinutes = string.IsNullOrWhiteSpace(server.Info.RestartAnnouncementMinutes) ? "15,5,1" : server.Info.RestartAnnouncementMinutes;
            ValidateOnRestart = server.Info.ValidateOnRestart;
            PinServerVersion = server.Info.PinServerVersion;
            UpdateOnStartRestart = server.Info.UpdateOnStartRestart;
            AutoUpdateCheckIntervalMinutes = Math.Max(5, server.Info.AutoUpdateCheckIntervalMinutes);
            HealthCheckEnabled = server.Info.HealthCheckEnabled;
            HealthCheckFailureThreshold = Math.Max(1, server.Info.HealthCheckFailureThreshold);
            HealthCheckIntervalSeconds = Math.Max(5, server.Info.HealthCheckIntervalSeconds);
            ServerPort = server.Info.Port;
            LaunchArguments = server.Info.LaunchArguments ?? "";

            // Keep existing V Rising installs on the same per-server settings and log paths
            // used by the dedicated editor. The user can still change these arguments later.
            if (server.AppId is "1829350" or "1604030" && string.IsNullOrWhiteSpace(LaunchArguments))
            {
                LaunchArguments = "-persistentDataPath .\\save-data -logFile .\\logs\\VRisingServer.log";
                server.Info.LaunchArguments = LaunchArguments;
                await Globals.WebAPI.UpdateServerInfoAsync(server.Info);
            }
            

            
            // Load Console
            ConsoleVM.IsEmbedded = true;
            var logPath = FindBestServerLog(server.Info.InstallPath) ?? Path.Combine(server.Info.InstallPath, "server.log");
            ServerLogPath = File.Exists(logPath) ? logPath : "Waiting for a server log file to be created.";
            ConsoleVM.Load(server.Name, logPath);
            GeneralLog = "";
            AppendGeneral("Launcher UI ready.");
            AppendGeneral($"Server root: {server.Info.InstallPath}");

            // Load Settings
            SettingsVM.IsEmbedded = true;
            await SettingsVM.LoadAsync(server.AppId, server.Name, server.Info.InstallPath);

            if (ServerPort <= 0)
            {
                var detectedPort = await Globals.Config.DetectServerPortAsync(server.AppId, server.Info.InstallPath);
                if (detectedPort.HasValue)
                {
                    ServerPort = detectedPort.Value;
                    server.Info.Port = detectedPort.Value;
                    await Globals.WebAPI.UpdateServerInfoAsync(server.Info);
                    DetailStatus = $"Detected server port {detectedPort.Value} from settings.";
                }
            }

            _monitorTimer.Start();
            
            // Initial loads
            LoadBackupsAsync(); // Fire and forget
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StartStopButtonContent))]
        [NotifyPropertyChangedFor(nameof(StartStopButtonBackground))]
        private bool _isRunning = false;

        public string StartStopButtonContent => IsRunning ? "STOP" : "START";
        public string StartStopButtonBackground => IsRunning ? "#F44336" : "#4CAF50"; // Red : Green


        [RelayCommand]
        public void GoBack()
        {
             // Avalonia Navigation logic needed.
             // Usually handled via Messenger or Event, or accessing ViewLocator resolved MainWindow ViewModel?
             // Since we don't have static access to MainWindow easily in Avalonia without casting ApplicationLifetime
             if (Avalonia.Application.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                 desktop.MainWindow.DataContext is MainViewModel mainVm)
             {
                 mainVm.CurrentView = mainVm.ServerGalleryVM;
                 _ = mainVm.ServerGalleryVM.RefreshCommand.ExecuteAsync(null);
             }
        }

        [RelayCommand]
        public void OpenServerFolder()
        {
            if (_currentServer == null) return;

            if (!Directory.Exists(_currentServer.Info.InstallPath))
            {
                DetailStatus = "Server folder not found.";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _currentServer.Info.InstallPath,
                    UseShellExecute = true
                });
                DetailStatus = "";
            }
            catch (Exception ex)
            {
                DetailStatus = $"Open folder failed: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task StartStopServer()
        {
            if (_currentServer == null) return;
            
            if (IsRunning)
            {
                Globals.ProcessManager.StopServer(_currentServer.AppId);
                DetailStatus = "Stop requested.";
                AppendGeneral("Stop requested.");
                if (BackupOnShutdown)
                {
                    await CreateBackupCoreAsync("Shutdown backup");
                }
            }
            else
            {
                if (UpdateOnStartRestart && AutoUpdateEnabled && !PinServerVersion)
                {
                    var updated = await RunSteamCmdOperationAsync("Update before start");
                    if (!updated) return;
                }

                if (BackupOnStartup)
                {
                    await CreateBackupCoreAsync("Startup backup");
                }

                var exePath = Globals.Executables.FindServerExecutable(_currentServer.Info.InstallPath);
                if (!string.IsNullOrEmpty(exePath))
                {
                    if (!await Globals.DirectX.EnsureLegacyRuntimeAsync(status => DetailStatus = status))
                    {
                        return;
                    }

                    Globals.ProcessManager.StartServer(_currentServer.AppId, exePath, LaunchArguments ?? "");
                    DetailStatus = "Start requested.";
                    AppendGeneral("Start requested.");
                }
                else
                {
                    DetailStatus = "Could not find a server executable in this folder.";
                }
            }
            UpdateStats();
        }

        [RelayCommand]
        public async Task RestartServer()
        {
            if (_currentServer == null) return;

            if (IsRunning)
            {
                Globals.ProcessManager.StopServer(_currentServer.AppId);
                AppendGeneral("Restart requested; waiting for server to stop.");
                if (BackupOnShutdown) await CreateBackupCoreAsync("Pre-restart backup");
                await Task.Delay(5000);
            }

            if ((ValidateOnRestart || UpdateOnStartRestart) && !PinServerVersion)
            {
                var updated = await RunSteamCmdOperationAsync(ValidateOnRestart ? "Validate before restart" : "Update before restart");
                if (!updated) return;
            }

            if (BackupOnStartup) await CreateBackupCoreAsync("Pre-start backup");

            var exePath = Globals.Executables.FindServerExecutable(_currentServer.Info.InstallPath);
            if (string.IsNullOrWhiteSpace(exePath))
            {
                DetailStatus = "Could not find a server executable in this folder.";
                return;
            }

            if (!await Globals.DirectX.EnsureLegacyRuntimeAsync(status => DetailStatus = status)) return;
            Globals.ProcessManager.StartServer(_currentServer.AppId, exePath, LaunchArguments ?? "");
            DetailStatus = "Restart completed.";
            AppendGeneral("Restart completed.");
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (_currentServer == null) return;
            
            var running = Globals.ProcessManager.IsRunning(_currentServer.AppId);
            var stats = Globals.ProcessManager.GetPerformance(_currentServer.AppId);
            
            Avalonia.Threading.Dispatcher.UIThread.Invoke(() => 
            {
                IsRunning = running;
                CpuUsage = $"{stats.CpuUsagePercent}%";
                MemoryUsage = $"{stats.MemoryUsageMb} MB";
                var uptime = Globals.ProcessManager.GetUptime(_currentServer.AppId);
                UptimeDisplay = running ? FormatDuration(uptime) : "-";
                NextRestartDisplay = GetNextRestartDisplay(running, uptime);
            });
        }

        [RelayCommand]
        public async Task DetectServerPort()
        {
            if (_currentServer == null) return;

            DetailStatus = "Looking for server port in settings files...";
            var detectedPort = await Globals.Config.DetectServerPortAsync(_currentServer.AppId, _currentServer.Info.InstallPath);

            if (detectedPort.HasValue)
            {
                ServerPort = detectedPort.Value;
                _currentServer.Info.Port = detectedPort.Value;
                await Globals.WebAPI.UpdateServerInfoAsync(_currentServer.Info);
                DetailStatus = $"Detected and saved server port {detectedPort.Value}.";
            }
            else
            {
                DetailStatus = "No server port found. You can enter it manually, then save automation settings.";
            }
        }

        [RelayCommand]
        public void ShowServerSettings() => IsSettingsMode = true;

        [RelayCommand]
        public void ShowServerDashboard() => IsSettingsMode = false;

        [RelayCommand]
        public async Task OpenPortCheck()
        {
            if (!IsPalworld || Avalonia.Application.Current?.ApplicationLifetime is not
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow == null) return;

            var dialog = new Views.PalworldPortCheckWindow();
            await dialog.ShowDialog(desktop.MainWindow);
        }

        private async Task<int> ResolveServerPortAsync()
        {
            if (_currentServer == null) return 0;

            var port = ServerPort > 0 ? ServerPort : _currentServer.Info.Port;
            if (port <= 0)
            {
                DetailStatus = "Looking for server port in settings files...";
                var detected = await Globals.Config.DetectServerPortAsync(_currentServer.AppId, _currentServer.Info.InstallPath);
                if (detected.HasValue) port = detected.Value;
            }

            if (port > 0 && port != _currentServer.Info.Port)
            {
                ServerPort = port;
                _currentServer.Info.Port = port;
                await Globals.WebAPI.UpdateServerInfoAsync(_currentServer.Info);
            }

            return port;
        }

        [RelayCommand]
        public void OpenPlayitDownload()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://playit.gg/download/windows",
                    UseShellExecute = true
                });
                TunnelStatus = "Download playit, run it once, claim the agent, then create a UDP tunnel to this server port.";
            }
            catch (Exception ex)
            {
                TunnelStatus = $"Could not open playit download page: {ex.Message}";
            }
        }

        [RelayCommand]
        public void OpenPlayitDashboard()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://playit.gg/account/tunnels",
                    UseShellExecute = true
                });
                TunnelStatus = "Create a UDP tunnel with local address 127.0.0.1 and the port shown in this app.";
            }
            catch (Exception ex)
            {
                TunnelStatus = $"Could not open playit dashboard: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task StartPlayitTunnel()
        {
            if (_currentServer == null) return;

            var port = await ResolveServerPortAsync();
            if (port <= 0)
            {
                TunnelStatus = "Detect or enter the server port before starting a tunnel.";
                return;
            }

            if (!await IsPlayitInstalledAsync())
            {
                TunnelStatus = "playit is not installed yet. Use Download playit, then run it and claim the agent.";
                return;
            }

            try
            {
                if (_playitProcess == null || _playitProcess.HasExited)
                {
                    _playitProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "playit",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    });
                }

                TunnelStatus = $"playit agent started. In the playit dashboard, create a UDP tunnel to 127.0.0.1:{port}, then share the playit address.";
            }
            catch (Exception ex)
            {
                TunnelStatus = $"Could not start playit: {ex.Message}";
            }
        }

        private static async Task<bool> IsPlayitInstalledAsync()
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c where playit")
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        public async Task SaveAutomationSettings()
        {
            if (_currentServer == null) return;
            
            _currentServer.Info.AutoRestart = AutoRestart;
            _currentServer.Info.AutoUpdateEnabled = AutoUpdateEnabled;
            _currentServer.Info.AutoUpdateSchedule = AutoUpdateSchedule;
            _currentServer.Info.AutoUpdateDay = AutoUpdateDay;
            _currentServer.Info.AutoBackupEnabled = AutoBackupEnabled;
            _currentServer.Info.AutoBackupIntervalHours = AutoBackupInterval;
            _currentServer.Info.AutoBackupIntervalMinutes = MinutesFromSchedule(BackupSchedule);
            _currentServer.Info.BackupOnStartup = BackupOnStartup;
            _currentServer.Info.BackupOnShutdown = BackupOnShutdown;
            _currentServer.Info.BackupRetentionDays = Math.Max(1, BackupRetentionDays);
            _currentServer.Info.BackupLocation = BackupLocation?.Trim() ?? "";
            _currentServer.Info.ScheduledRestartEnabled = ScheduledRestartEnabled;
            _currentServer.Info.ScheduledRestartIntervalHours = ScheduledRestartInterval;
            _currentServer.Info.ScheduledRestartTime = ScheduledRestartTime?.Trim() ?? "03:00 AM";
            _currentServer.Info.RestartMinimumUptimeHours = Math.Max(0, RestartMinimumUptimeHours);
            _currentServer.Info.AnnounceRestarts = AnnounceRestarts;
            _currentServer.Info.RestartAnnouncementMinutes = RestartAnnouncementMinutes?.Trim() ?? "15,5,1";
            _currentServer.Info.ValidateOnRestart = ValidateOnRestart;
            _currentServer.Info.PinServerVersion = PinServerVersion;
            _currentServer.Info.UpdateOnStartRestart = UpdateOnStartRestart;
            _currentServer.Info.AutoUpdateCheckIntervalMinutes = Math.Max(5, AutoUpdateCheckIntervalMinutes);
            _currentServer.Info.HealthCheckEnabled = HealthCheckEnabled;
            _currentServer.Info.HealthCheckFailureThreshold = Math.Max(1, HealthCheckFailureThreshold);
            _currentServer.Info.HealthCheckIntervalSeconds = Math.Max(5, HealthCheckIntervalSeconds);
            _currentServer.Info.Port = ServerPort;
            _currentServer.Info.LaunchArguments = LaunchArguments ?? "";

            if (ServerPort < 0 || ServerPort > 65535)
            {
                DetailStatus = "Server port must be between 0 and 65535.";
                return;
            }

            if (ServerPort > 0 && Globals.Network.IsUdpPortInUse(ServerPort) && !IsRunning)
            {
                DetailStatus = $"Warning: UDP port {ServerPort} appears to be in use. Settings saved anyway.";
            }

            
            await Globals.WebAPI.UpdateServerInfoAsync(_currentServer.Info);
            await Globals.Automation.TickAsync();
            if (!DetailStatus.StartsWith("Warning:"))
            {
                DetailStatus = "Server automation settings saved.";
            }
            AppendGeneral("Automation settings saved.");
        }

        [ObservableProperty]
        private bool _isInstalled;

        [RelayCommand]
        public async Task UpdateServer()
        {
            await RunSteamCmdOperationAsync("Update / Validate");
        }

        [RelayCommand]
        public void ClearSteamCmdLog()
        {
            SteamCmdLog = "";
        }

        private async Task<bool> RunSteamCmdOperationAsync(string operation)
        {
            if (_currentServer == null) return false;

            IsInstalling = true;
            InstallStatus = $"{operation} starting...";
            InstallProgress = 0;
            AppendSteamCmd($"{operation} requested for app {_currentServer.AppId}.");

            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                await Globals.SteamCMD.InstallServerAsync(
                    _currentServer.AppId,
                    _currentServer.Info.InstallPath,
                    status => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        InstallStatus = status;
                        AppendSteamCmd(status);
                        if (status.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
                            status.Contains("Downloaded", StringComparison.OrdinalIgnoreCase))
                        {
                            InstallProgress = Math.Min(95, InstallProgress + 1);
                        }
                    }),
                    success => tcs.TrySetResult(success));

                var finalSuccess = await tcs.Task;
                if (!finalSuccess) throw new Exception("SteamCMD operation failed. Review the SteamCMD tab for details.");

                InstallStatus = $"{operation} completed successfully.";
                InstallProgress = 100;
                AppendSteamCmd($"{operation} completed successfully.");
                AppendGeneral($"{operation} completed.");

                if (Directory.Exists(_currentServer.Info.InstallPath))
                {
                    _currentServer.Info.IsInstalled = true;
                    IsInstalled = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                InstallStatus = $"Error: {ex.Message}";
                AppendSteamCmd(InstallStatus);
                AppendGeneral($"{operation} failed: {ex.Message}");
                return false;
            }
            finally
            {
                IsInstalling = false;
            }
        }

        [RelayCommand]
        public async Task UninstallServer()
        {
            if (_currentServer == null) return;

            var confirmed = await Globals.Dialogs.ConfirmAsync(
                "Remove server files",
                $"This will stop the server, delete all files in:\n{_currentServer.Info.InstallPath}\n\nThis cannot be undone.",
                "Delete Files");

            if (!confirmed) return;

            try
            {
                if (IsRunning)
                {
                    Globals.ProcessManager.StopServer(_currentServer.AppId);
                    await Task.Delay(2000); // Give it time to stop
                }

                if (Directory.Exists(_currentServer.Info.InstallPath))
                {
                    await Task.Run(() => Directory.Delete(_currentServer.Info.InstallPath, true));
                }

                _currentServer.Info.IsInstalled = false;
                IsInstalled = false;
                await Globals.WebAPI.RemoveCustomServerAsync(_currentServer.AppId);
                DetailStatus = "Server removed.";

                if (Avalonia.Application.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow.DataContext is MainViewModel mainVm)
                {
                    await mainVm.ServerGalleryVM.RefreshCommand.ExecuteAsync(null);
                    mainVm.CurrentView = mainVm.ServerGalleryVM;
                }
            }
            catch (Exception ex)
            {
                DetailStatus = $"Remove failed: {ex.Message}";
            }
        }
        [RelayCommand]
        public async Task LoadBackupsAsync()
        {
            if (_currentServer == null) return;
            try
            {
                var list = await Globals.Backups.GetBackupsAsync(_currentServer.Info.InstallPath, BackupLocation);
                
                Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
                {
                    Backups.Clear();
                    foreach (var b in list) Backups.Add(b);
                });
            }
            catch { }
        }

        [RelayCommand]
        public async Task CreateBackup()
        {
            await CreateBackupCoreAsync("Manual backup");
        }

        private async Task<bool> CreateBackupCoreAsync(string operation)
        {
            if (_currentServer == null) return false;

            try
            {
                DetailStatus = $"{operation} in progress...";
                await Globals.Backups.CreateBackupAsync(_currentServer.Name, _currentServer.Info.InstallPath, BackupLocation);
                await Globals.Backups.PruneBackupsOlderThanAsync(
                    _currentServer.Info.InstallPath,
                    Math.Max(1, BackupRetentionDays),
                    BackupLocation);
                await LoadBackupsAsync();
                DetailStatus = $"{operation} completed.";
                AppendGeneral($"{operation} completed.");
                return true;
            }
            catch (Exception ex)
            {
                DetailStatus = $"{operation} failed: {ex.Message}";
                AppendGeneral(DetailStatus);
                return false;
            }
        }

        [RelayCommand]
        public async Task RestoreBackup(Services.BackupInfo backup)
        {
            if (backup == null || _currentServer == null) return;

            var confirmed = await Globals.Dialogs.ConfirmAsync(
                "Restore backup",
                $"Restore '{backup.Name}'?\n\nCurrent server files may be overwritten.",
                "Restore");

            if (!confirmed) return;

            try
            {
                // Stop server if running
                if (IsRunning)
                {
                    Globals.ProcessManager.StopServer(_currentServer.AppId);
                }

                await Globals.Backups.RestoreBackupAsync(backup.FullPath, _currentServer.Info.InstallPath);
                DetailStatus = "Backup restored.";
            }
            catch (Exception ex)
            {
                DetailStatus = $"Restore failed: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task DeleteBackup(Services.BackupInfo backup)
        {
            if (backup == null) return;

             var confirmed = await Globals.Dialogs.ConfirmAsync(
                 "Delete backup",
                 $"Delete backup '{backup.Name}'?\n\nThis cannot be undone.",
                 "Delete");

             if (!confirmed) return;

             try 
             {
                 await Globals.Backups.DeleteBackupAsync(backup.FullPath);
                 await LoadBackupsAsync();
                 DetailStatus = "Backup deleted.";
             }
             catch (Exception ex)
             {
                 DetailStatus = $"Delete failed: {ex.Message}";
             }
        }

        [RelayCommand]
        public void OpenBackupFolder(Services.BackupInfo backup)
        {
            if (backup == null || !File.Exists(backup.FullPath)) return;
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{backup.FullPath}\"");
            }
            catch (Exception ex)
            {
                // MessageBox.Show($"Could not open folder: {ex.Message}", "Error");
            }
        }

        [RelayCommand]
        public void OpenBackupLocation()
        {
            if (_currentServer == null) return;

            try
            {
                var folder = Globals.Backups.GetBackupDirectory(_currentServer.Info.InstallPath, BackupLocation);
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                DetailStatus = $"Could not open backup location: {ex.Message}";
            }
        }

        [RelayCommand]
        public void RefreshServerLog()
        {
            if (_currentServer == null) return;

            var logPath = FindBestServerLog(_currentServer.Info.InstallPath);
            if (string.IsNullOrWhiteSpace(logPath))
            {
                ServerLogPath = "No server log detected. Start the server, then refresh.";
                return;
            }

            ServerLogPath = logPath;
            ConsoleVM.Load(_currentServer.Name, logPath);
            AppendGeneral($"Attached server log: {logPath}");
        }

        [RelayCommand]
        public void RefreshPlayers()
        {
            PlayersStatus = "This server has no configured query/RCON adapter. Process monitoring still works, but player lists require a game-specific protocol.";
        }

        [RelayCommand]
        public void RefreshChat()
        {
            ChatStatus = "This server has no configured RCON/REST chat adapter. Chat is shown only when a game-specific protocol is available.";
        }

        private void AppendGeneral(string message)
        {
            GeneralLog = AppendCapped(GeneralLog, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private void AppendSteamCmd(string message)
        {
            SteamCmdLog = AppendCapped(SteamCmdLog, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        private static string AppendCapped(string current, string addition)
        {
            var combined = (current ?? "") + addition;
            return combined.Length > 120000 ? combined[^100000..] : combined;
        }

        private string GetNextRestartDisplay(bool running, TimeSpan uptime)
        {
            if (!running || !ScheduledRestartEnabled || !DateTime.TryParse(ScheduledRestartTime, out var parsed)) return "off";
            if (uptime < TimeSpan.FromHours(Math.Max(0, RestartMinimumUptimeHours)))
            {
                return $"after {RestartMinimumUptimeHours}h uptime";
            }

            var target = DateTime.Today.Add(parsed.TimeOfDay);
            if (target <= DateTime.Now) target = target.AddDays(1);
            return target.ToString("ddd h:mm tt");
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1) return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
            if (duration.TotalHours >= 1) return $"{(int)duration.TotalHours}h {duration.Minutes}m";
            return $"{Math.Max(0, duration.Minutes)}m";
        }

        private static int MinutesFromSchedule(string schedule)
        {
            if (string.IsNullOrWhiteSpace(schedule)) return 1440;
            var value = schedule.Trim().ToLowerInvariant();
            if (value.EndsWith("m") && int.TryParse(value[..^1], out var minutes)) return Math.Max(15, minutes);
            if (value.EndsWith("h") && int.TryParse(value[..^1], out var hours)) return Math.Max(1, hours) * 60;
            return 1440;
        }

        private static string ScheduleFromMinutes(int minutes)
        {
            if (minutes < 60) return $"{minutes}m";
            return $"{Math.Max(1, minutes / 60)}h";
        }

        private static string? FindBestServerLog(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath)) return null;

            try
            {
                var candidates = Directory.EnumerateFiles(installPath, "*", SearchOption.AllDirectories)
                    .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Backups{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    .Where(path =>
                    {
                        var extension = Path.GetExtension(path);
                        var name = Path.GetFileName(path);
                        return extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("server.log", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("console", StringComparison.OrdinalIgnoreCase) && extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList();

                return candidates.FirstOrDefault()?.FullName;
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Warn($"Could not scan server logs in {installPath}: {ex.Message}");
                return null;
            }
        }



    }
}
