using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Services;

namespace SteamServerBuddy.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _totalServers;

        [ObservableProperty]
        private int _runningServers;

        [ObservableProperty]
        private string _steamCMDStatus = "Checking...";

        [ObservableProperty]
        private bool _isSteamCMDInstalled;

        // Host-Level Metrics
        [ObservableProperty]
        private double _cpuPercent;

        [ObservableProperty]
        private string _cpuColor = "#48BB78";

        [ObservableProperty]
        private double _ramPercent;

        [ObservableProperty]
        private double _ramUsedGB;

        [ObservableProperty]
        private double _ramTotalGB;

        [ObservableProperty]
        private string _ramColor = "#48BB78";

        [ObservableProperty]
        private double _diskPercent;

        [ObservableProperty]
        private double _diskFreeGB;

        [ObservableProperty]
        private double _diskTotalGB;

        [ObservableProperty]
        private string _diskDrive = "C:";

        [ObservableProperty]
        private string _diskColor = "#48BB78";

        [ObservableProperty]
        private double _netInMbps;

        [ObservableProperty]
        private double _netOutMbps;

        [ObservableProperty]
        private string _uptime = "0d 0h";

        // Process Metrics for running servers
        public ObservableCollection<ProcessMetricsViewModel> RunningProcesses { get; } = new();

        private readonly ServersViewModel _serversVM;
        private readonly SystemMonitorService _monitor;
        private readonly System.Timers.Timer _monitorTimer;
        private bool _isWindowFocused = true;

        public DashboardViewModel(ServersViewModel serversVM)
        {
            _serversVM = serversVM;
            _monitor = new SystemMonitorService();
            
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            FixSteamCMDCommand = new AsyncRelayCommand(FixSteamCMDAsync);
            KillProcessCommand = new RelayCommand<int>(KillProcess);

            // Setup monitoring timer (1 second interval)
            _monitorTimer = new System.Timers.Timer(1000);
            _monitorTimer.Elapsed += (s, e) => UpdateMetrics();
            _monitorTimer.Start();

            // Track window focus to pause monitoring when minimized
            Application.Current.Activated += (s, e) => _isWindowFocused = true;
            Application.Current.Deactivated += (s, e) => _isWindowFocused = false;
        }

        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand FixSteamCMDCommand { get; }
        public IRelayCommand<int> KillProcessCommand { get; }

        private void UpdateMetrics()
        {
            // Skip if window not focused to save CPU
            if (!_isWindowFocused) return;

            try
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // CPU
                    CpuPercent = Math.Round(_monitor.GetCpuPercent(), 1);
                    CpuColor = SystemMonitorService.GetThresholdColor(CpuPercent);

                    // RAM
                    var ram = _monitor.GetRamInfo();
                    RamUsedGB = ram.UsedGB;
                    RamTotalGB = ram.TotalGB;
                    RamPercent = ram.Percent;
                    RamColor = SystemMonitorService.GetThresholdColor(RamPercent);

                    // Disk
                    var disk = _monitor.GetDiskInfo(Globals.AppSettings.GetServerInstallPath());
                    DiskFreeGB = disk.FreeGB;
                    DiskTotalGB = disk.TotalGB;
                    DiskPercent = disk.Percent;
                    DiskDrive = disk.DriveLetter;
                    DiskColor = SystemMonitorService.GetThresholdColor(DiskPercent);

                    // Network
                    var net = _monitor.GetNetworkIO();
                    NetInMbps = net.InMbps;
                    NetOutMbps = net.OutMbps;

                    // Uptime
                    Uptime = _monitor.GetUptime();

                    // Update process metrics for running servers
                    UpdateProcessMetrics();
                });
            }
            catch { }
        }

        private void UpdateProcessMetrics()
        {
            try
            {
                var runningServers = _serversVM.Servers.Where(s => s.IsRunning).ToList();
                
                // Remove processes that are no longer running
                var toRemove = RunningProcesses
                    .Where(p => !runningServers.Any(s => s.Name == p.ServerName))
                    .ToList();
                foreach (var p in toRemove) RunningProcesses.Remove(p);

                // Update or add process metrics
                foreach (var server in runningServers)
                {
                    var process = GetServerProcess(server.Info.InstallPath);
                    if (process == null) continue;

                    var metrics = _monitor.GetProcessMetrics(process);
                    if (metrics == null) continue;

                    var existing = RunningProcesses.FirstOrDefault(p => p.ServerName == server.Name);
                    if (existing != null)
                    {
                        existing.Update(metrics);
                    }
                    else
                    {
                        RunningProcesses.Add(new ProcessMetricsViewModel(server.Name, metrics));
                    }
                }
            }
            catch { }
        }

        private System.Diagnostics.Process GetServerProcess(string installPath)
        {
            try
            {
                // Find exe in install path
                var exes = System.IO.Directory.GetFiles(installPath, "*.exe", System.IO.SearchOption.AllDirectories)
                    .Where(f => !f.Contains("steam", StringComparison.OrdinalIgnoreCase) &&
                                !f.Contains("redist", StringComparison.OrdinalIgnoreCase) &&
                                !f.Contains("crash", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var exe in exes)
                {
                    var processName = System.IO.Path.GetFileNameWithoutExtension(exe);
                    var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                    if (processes.Any())
                    {
                        return processes.First();
                    }
                }
            }
            catch { }
            return null;
        }

        private void KillProcess(int pid)
        {
            try
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to kill process {pid}?",
                    "Confirm Kill",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _monitor.KillProcess(pid);
                }
            }
            catch { }
        }

        public async Task FixSteamCMDAsync()
        {
            SteamCMDStatus = "Setting up SteamCMD...";
            await Globals.SteamCMD.EnsureSteamCMDAsync(status => SteamCMDStatus = status);
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            // Update counts from ServersViewModel if loaded
            TotalServers = _serversVM.Servers.Count;
            RunningServers = _serversVM.Servers.Count(s => s.IsRunning);

            // Check SteamCMD
            var steamCmdPath = Globals.SteamCMD.GetSteamCMDPath();
            IsSteamCMDInstalled = System.IO.File.Exists(steamCmdPath);
            SteamCMDStatus = IsSteamCMDInstalled ? "Installed & Ready" : "Not Found (Click Fix)";

            await Task.CompletedTask;
        }

        public void Cleanup()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
        }
    }

    public partial class ProcessMetricsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _serverName;

        [ObservableProperty]
        private int _pid;

        [ObservableProperty]
        private double _cpuPercent;

        [ObservableProperty]
        private string _cpuColor = "#48BB78";

        [ObservableProperty]
        private double _workingSetMB;

        [ObservableProperty]
        private double _privateBytesMB;

        [ObservableProperty]
        private int _threadCount;

        [ObservableProperty]
        private int _handleCount;

        [ObservableProperty]
        private string _runningTime;

        public ProcessMetricsViewModel(string serverName, ProcessMetrics metrics)
        {
            ServerName = serverName;
            Update(metrics);
        }

        public void Update(ProcessMetrics metrics)
        {
            Pid = metrics.Pid;
            CpuPercent = metrics.CpuPercent;
            CpuColor = SystemMonitorService.GetThresholdColor(CpuPercent);
            WorkingSetMB = metrics.WorkingSetMB;
            PrivateBytesMB = metrics.PrivateBytesMB;
            ThreadCount = metrics.ThreadCount;
            HandleCount = metrics.HandleCount;
            RunningTime = metrics.RunningTime;
        }
    }
}
