using System;
using System.Diagnostics;
using System.IO;
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
        private string _appId;

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

        private DateTime _lastServerStartTime;
        private bool _warningSent = false;
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
            ScheduledRestartEnabled = server.Info.ScheduledRestartEnabled;
            ScheduledRestartInterval = server.Info.ScheduledRestartIntervalHours;
            ServerPort = server.Info.Port;
            LaunchArguments = server.Info.LaunchArguments ?? "";
            

            
            // Load Console
            ConsoleVM.IsEmbedded = true;
            var logPath = Path.Combine(server.Info.InstallPath, "server.log");
            ConsoleVM.Load(server.Name, logPath);

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
            }
            else
            {
                    var exePath = Globals.Executables.FindServerExecutable(_currentServer.Info.InstallPath);
                if (!string.IsNullOrEmpty(exePath))
                {
                    Globals.ProcessManager.StartServer(_currentServer.AppId, exePath, LaunchArguments ?? "");
                    DetailStatus = "Start requested.";
                }
                else
                {
                    DetailStatus = "Could not find a server executable in this folder.";
                }
            }
            // Reset timers on state change
            if (IsRunning) 
            {
                 _lastServerStartTime = DateTime.Now;
                 _warningSent = false;
            }
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
            });

            // Scheduled Restart Logic
            if (running && ScheduledRestartEnabled && ScheduledRestartInterval > 0)
            {
                var elapsed = DateTime.Now - _lastServerStartTime;
                var remainingHours = ScheduledRestartInterval - elapsed.TotalHours;

                // Warning (5 mins before)
                if (remainingHours <= (5.0 / 60.0) && remainingHours > 0 && !_warningSent)
                {
                     _warningSent = true;
                     if (_currentServer.Info.EnableDiscordAlerts)
                     {
                         _ = Globals.Notification.SendDiscordAlertAsync(
                             _currentServer.Info.DiscordWebhookUrl, 
                             $"Scheduled restart: server '{_currentServer.Name}' will restart in 5 minutes."); 
                     }
                }

                // Restart
                if (remainingHours <= 0)
                {
                    _lastServerStartTime = DateTime.Now; // Reset immediately to prevent double restart
                    _warningSent = false;
                    
                    Avalonia.Threading.Dispatcher.UIThread.Invoke(() => 
                    {
                        // Stop
                        Globals.ProcessManager.StopServer(_currentServer.AppId);
                        
                        // Restart after delay
                        Task.Run(async () => 
                        {
                            await Task.Delay(5000); // Wait 5s
                             Avalonia.Threading.Dispatcher.UIThread.Invoke(() => _ = StartStopServer()); // Call Start again
                        });
                    });
                }
            }
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
            _currentServer.Info.ScheduledRestartEnabled = ScheduledRestartEnabled;
            _currentServer.Info.ScheduledRestartIntervalHours = ScheduledRestartInterval;
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
            if (!DetailStatus.StartsWith("Warning:"))
            {
                DetailStatus = "Automation settings saved.";
            }
        }

        [ObservableProperty]
        private bool _isInstalled;

        [RelayCommand]
        public async Task UpdateServer()
        {
            if (_currentServer == null) return;

            IsInstalling = true;
            InstallStatus = "Starting SteamCMD...";
            InstallProgress = 0;

            try
            {
                var tcs = new TaskCompletionSource<bool>();
                await Globals.SteamCMD.InstallServerAsync(
                    _currentServer.AppId,
                    _currentServer.Info.InstallPath,
                    status =>
                    {
                        InstallStatus = status;
                        if (status.Contains("progress") || status.Contains("Downloaded"))
                        {
                            InstallProgress += 1;
                            if (InstallProgress > 95) InstallProgress = 95;
                        }
                    },
                    success =>
                    {
                        tcs.SetResult(success);
                    });

                bool finalSuccess = await tcs.Task;
                if (!finalSuccess) throw new Exception("SteamCMD update failed.");

                InstallStatus = "Server updated successfully!";
                InstallProgress = 100;
                
                // Refresh installed status
                if (Directory.Exists(_currentServer.Info.InstallPath))
                {
                    _currentServer.Info.IsInstalled = true;
                    IsInstalled = true;
                }
            }
            catch (Exception ex)
            {
                InstallStatus = $"Error: {ex.Message}";
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
                var list = await Globals.Backups.GetBackupsAsync(_currentServer.Info.InstallPath);
                
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
            if (_currentServer == null) return;
            // ideally show loading indicator
            try 
            {
                await Globals.Backups.CreateBackupAsync(_currentServer.Name, _currentServer.Info.InstallPath);
                await LoadBackupsAsync();
                DetailStatus = "Backup created.";
            }
            catch (Exception ex)
            {
                DetailStatus = $"Backup failed: {ex.Message}";
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



    }
}
