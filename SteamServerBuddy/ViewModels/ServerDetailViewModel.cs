using System;
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

        // Notification Toggles



        private System.Timers.Timer _monitorTimer;
        private ServerItemViewModel _currentServer;

        public ServerDetailViewModel()
        {
            _monitorTimer = new System.Timers.Timer(2000); // 2 seconds update
            _monitorTimer.Elapsed += (s, e) => UpdateStats();
        }

        public async Task LoadAsync(ServerItemViewModel server)
        {
            ServerName = server.Name;
            AppId = server.AppId;
            _currentServer = server;
            IsInstalled = server.Info.IsInstalled;

            // Load automation settings
            AutoRestart = server.Info.AutoRestart;
            AutoUpdateEnabled = server.Info.AutoUpdateEnabled;
            AutoUpdateSchedule = !string.IsNullOrEmpty(server.Info.AutoUpdateSchedule) ? server.Info.AutoUpdateSchedule : "04:00 AM";
            AutoUpdateDay = !string.IsNullOrEmpty(server.Info.AutoUpdateDay) ? server.Info.AutoUpdateDay : "Daily";
            AutoBackupEnabled = server.Info.AutoBackupEnabled;
            AutoBackupInterval = server.Info.AutoBackupIntervalHours;
            

            
            // Load Console
            ConsoleVM.IsEmbedded = true;
            var logPath = Path.Combine(server.Info.InstallPath, "server.log");
            ConsoleVM.Load(server.Name, logPath);

            // Load Settings
            SettingsVM.IsEmbedded = true;
            await SettingsVM.LoadAsync(server.AppId, server.Name, server.Info.InstallPath);

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
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                // Navigate back to the gallery
                mainVm.CurrentView = mainVm.ServerGalleryVM;
            }
        }

        [RelayCommand]
        public void StartStopServer()
        {
            if (_currentServer == null) return;
            
            if (IsRunning)
            {
                Globals.ProcessManager.StopServer(_currentServer.AppId);
            }
            else
            {
                var exePath = FindServerExecutable(_currentServer.Info.InstallPath);
                if (!string.IsNullOrEmpty(exePath))
                {
                    Globals.ProcessManager.StartServer(_currentServer.AppId, exePath);
                }
                else
                {
                    System.Windows.MessageBox.Show("Could not find server executable (.exe/start.bat)", "Error");
                }
            }
            UpdateStats(); 
        }

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

        private void UpdateStats()
        {
            if (_currentServer == null) return;
            
            var running = Globals.ProcessManager.IsRunning(_currentServer.AppId);
            var stats = Globals.ProcessManager.GetPerformance(_currentServer.AppId);
            
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                IsRunning = running;
                CpuUsage = $"{stats.CpuUsagePercent}%";
                MemoryUsage = $"{stats.MemoryUsageMb} MB";
            });
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
            

            
            await Globals.WebAPI.UpdateServerInfoAsync(_currentServer.Info);
            System.Windows.MessageBox.Show("Automation settings saved!", "Success");
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

            var res = System.Windows.MessageBox.Show($"Are you sure you want to uninstall this server?\nThis will DELETE ALL FILES in:\n{_currentServer.Info.InstallPath}", 
                                                     "Confirm Uninstall", 
                                                     System.Windows.MessageBoxButton.YesNo, 
                                                     System.Windows.MessageBoxImage.Warning);
            
            if (res != System.Windows.MessageBoxResult.Yes) return;

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
                System.Windows.MessageBox.Show("Server uninstalled successfully.", "Success");
                
                // Optionally go back since there's nothing to see? 
                // Or stay here to allow Re-Install. User request implies staying is fine.
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Uninstall failed: {ex.Message}", "Error");
            }
        }
        [RelayCommand]
        public async Task LoadBackupsAsync()
        {
            if (_currentServer == null) return;
            try
            {
                var list = await Globals.Backups.GetBackupsAsync(_currentServer.Info.InstallPath);
                
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
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
                System.Windows.MessageBox.Show("Backup created successfully!", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Backup failed: {ex.Message}", "Error");
            }
        }

        [RelayCommand]
        public async Task RestoreBackup(Services.BackupInfo backup)
        {
            if (backup == null || _currentServer == null) return;
            
            var res = System.Windows.MessageBox.Show($"Are you sure you want to restore '{backup.Name}'?\nThis will overwrite current files.", "Confirm Restore", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
            if (res != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                // Stop server if running
                if (IsRunning)
                {
                    Globals.ProcessManager.StopServer(_currentServer.AppId);
                }

                await Globals.Backups.RestoreBackupAsync(backup.FullPath, _currentServer.Info.InstallPath);
                System.Windows.MessageBox.Show("Backup restored successfully!", "Success");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Restore failed: {ex.Message}", "Error");
            }
        }

        [RelayCommand]
        public async Task DeleteBackup(Services.BackupInfo backup)
        {
            if (backup == null) return;
             var res = System.Windows.MessageBox.Show($"Delete backup '{backup.Name}'?", "Confirm Delete", System.Windows.MessageBoxButton.YesNo);
             if (res != System.Windows.MessageBoxResult.Yes) return;

             try 
             {
                 await Globals.Backups.DeleteBackupAsync(backup.FullPath);
                 await LoadBackupsAsync();
             }
             catch (Exception ex)
             {
                 System.Windows.MessageBox.Show($"Delete failed: {ex.Message}", "Error");
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
                System.Windows.MessageBox.Show($"Could not open folder: {ex.Message}", "Error");
            }
        }



    }
}
