using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Models;
using SteamServerBuddy.Services;

namespace SteamServerBuddy.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ServersViewModel _serversVM;
        private readonly SystemMonitorService _monitor;
        private readonly System.Timers.Timer _monitorTimer;

        [ObservableProperty] private int _totalServers;
        [ObservableProperty] private int _runningServers;
        [ObservableProperty] private int _stoppedServers;
        [ObservableProperty] private int _upToDateServers;
        [ObservableProperty] private string _steamCMDStatus = "Checking...";
        [ObservableProperty] private bool _isSteamCMDInstalled;
        [ObservableProperty] private string _uptime = "0d 0h";
        [ObservableProperty] private double _netInMbps;
        [ObservableProperty] private double _netOutMbps;
        [ObservableProperty] private string _activeTasks = "0";

        public ObservableCollection<DashboardServerCardViewModel> ServerCards { get; } = new();
        public ObservableCollection<ActivityLogItemViewModel> ActivityLog { get; } = new();
        public DashboardViewModel(ServersViewModel serversVM)
        {
            _serversVM = serversVM;
            _monitor = new SystemMonitorService();

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            FixSteamCMDCommand = new AsyncRelayCommand(FixSteamCMDAsync);
            NavigateAddCommand = new RelayCommand(NavigateAdd);
            ViewAllServersCommand = new RelayCommand(NavigateServers);
            ClearActivityCommand = new RelayCommand(() => ActivityLog.Clear());
            OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);

            AddActivity("Dashboard ready.");

            _monitorTimer = new System.Timers.Timer(3000);
            _monitorTimer.Elapsed += async (_, _) => await RefreshRuntimeAsync();
            _monitorTimer.Start();
        }

        public IAsyncRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand FixSteamCMDCommand { get; }
        public IRelayCommand NavigateAddCommand { get; }
        public IRelayCommand ViewAllServersCommand { get; }
        public IRelayCommand ClearActivityCommand { get; }
        public IRelayCommand OpenLogsFolderCommand { get; }

        public async Task RefreshAsync()
        {
            var servers = await Globals.WebAPI.FetchDedicatedServersAsync();

            ServerCards.Clear();
            foreach (var server in servers.Take(6))
            {
                var card = new DashboardServerCardViewModel(server, AddActivity);
                ServerCards.Add(card);
                _ = card.LoadImageAsync();
            }

            TotalServers = servers.Count;
            RunningServers = servers.Count(s => Globals.ProcessManager.IsRunning(s.AppId));
            StoppedServers = Math.Max(0, TotalServers - RunningServers);
            UpToDateServers = TotalServers;

            await _serversVM.RefreshCommand.ExecuteAsync(null);

            var steamCmdPath = Globals.SteamCMD.GetSteamCMDPath();
            IsSteamCMDInstalled = File.Exists(steamCmdPath);
            SteamCMDStatus = IsSteamCMDInstalled ? "Up to date" : "Not installed";

            await RefreshRuntimeAsync();
        }

        private async Task RefreshRuntimeAsync()
        {
            try
            {
                var net = _monitor.GetNetworkIO();
                NetInMbps = net.InMbps;
                NetOutMbps = net.OutMbps;
                Uptime = _monitor.GetUptime();

                foreach (var card in ServerCards)
                {
                    card.RefreshStatus();
                }

                RunningServers = ServerCards.Count(c => c.IsRunning);
                StoppedServers = Math.Max(0, TotalServers - RunningServers);
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error("Dashboard runtime refresh failed", ex);
            }
        }

        public async Task FixSteamCMDAsync()
        {
            SteamCMDStatus = "Setting up...";
            AddActivity("SteamCMD setup started.");
            await Globals.SteamCMD.EnsureSteamCMDAsync(status => SteamCMDStatus = status);
            AddActivity("SteamCMD setup finished.");
            await RefreshAsync();
        }

        private void AddActivity(string message)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ActivityLog.Insert(0, new ActivityLogItemViewModel(DateTime.Now, message));
                while (ActivityLog.Count > 80) ActivityLog.RemoveAt(ActivityLog.Count - 1);
            });
        }

        private static void NavigateAdd()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.Navigate("Add");
            }
        }

        private static void NavigateServers()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.DataContext is MainViewModel mainVm)
            {
                mainVm.Navigate("Servers");
            }
        }

        private static void OpenLogsFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppPaths.DataDir,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public void Cleanup()
        {
            _monitorTimer?.Stop();
            _monitorTimer?.Dispose();
        }
    }

    public partial class DashboardServerCardViewModel : ObservableObject
    {
        private static readonly HttpClient ImageClient = new();
        private readonly Action<string> _activity;

        public ServerInfo Info { get; }
        public string Name => Info.DisplayName;
        public string AppId => Info.AppId;
        public string UptimeDisplay => IsRunning ? "Running" : "Stopped";
        public string StatusLabel => IsRunning ? "Running" : "Stopped";
        public string StatusColor => IsRunning ? "#35D07F" : "#4A5568";
        public bool CanStop => IsRunning;

        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private Bitmap _imageSource;

        public DashboardServerCardViewModel(ServerInfo info, Action<string> activity)
        {
            Info = info;
            _activity = activity;
            RefreshStatus();
        }

        public IRelayCommand StartCommand => new RelayCommand(Start);
        public IRelayCommand StopCommand => new RelayCommand(Stop);
        public IRelayCommand RestartCommand => new RelayCommand(Restart);
        public IAsyncRelayCommand DetailsCommand => new AsyncRelayCommand(OpenDetailsAsync);

        public void RefreshStatus()
        {
            IsRunning = Globals.ProcessManager.IsRunning(AppId);
            OnPropertyChanged(nameof(UptimeDisplay));
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(CanStop));
        }

        public async Task LoadImageAsync()
        {
            var url = !string.IsNullOrWhiteSpace(Info.HeaderImageUrl)
                ? Info.HeaderImageUrl
                : $"https://cdn.cloudflare.steamstatic.com/steam/apps/{AppId}/header.jpg";

            try
            {
                var bytes = await ImageClient.GetByteArrayAsync(url);
                using var stream = new MemoryStream(bytes);
                ImageSource = new Bitmap(stream);
            }
            catch
            {
                // Art is nice-to-have; cards still work without it.
            }
        }

        private void Start()
        {
            var exe = Globals.Executables.FindServerExecutable(Info.InstallPath);
            if (string.IsNullOrWhiteSpace(exe))
            {
                _activity($"Could not find executable for {Name}.");
                return;
            }

            Globals.ProcessManager.StartServer(AppId, exe, Info.LaunchArguments ?? "");
            _activity($"Started {Name}.");
            RefreshStatus();
        }

        private void Stop()
        {
            Globals.ProcessManager.StopServer(AppId);
            _activity($"Stopped {Name}.");
            RefreshStatus();
        }

        private void Restart()
        {
            Stop();
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                Avalonia.Threading.Dispatcher.UIThread.Post(Start);
            });
            _activity($"Restart requested for {Name}.");
        }

        private async Task OpenDetailsAsync()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.DataContext is MainViewModel mainVm)
            {
                await mainVm.OpenDetailCommand.ExecuteAsync(new ServerItemViewModel(Info));
            }
        }
    }

    public class ActivityLogItemViewModel
    {
        public ActivityLogItemViewModel(DateTime time, string message)
        {
            Time = time;
            Message = message;
        }

        public DateTime Time { get; }
        public string Message { get; }
        public string Display => $"[{Time:HH:mm:ss}]  {Message}";
    }

}
