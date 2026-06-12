using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Models;
using SteamServerBuddy.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Timers;
using Avalonia; // For Application access if needed, though strictly we should avoid it in VM

namespace SteamServerBuddy.ViewModels
{
    public partial class ServersViewModel : ObservableObject
    {
        public ObservableCollection<ServerItemViewModel> Servers { get; } = new ObservableCollection<ServerItemViewModel>();
        
        [ObservableProperty]
        private bool _isLoading;

        private System.Timers.Timer _monitorTimer;

        public ServersViewModel()
        {
            RefreshCommand.Execute(null);
            // Optimized: Poll every 5 seconds instead of 2 (less CPU usage)
            _monitorTimer = new System.Timers.Timer(5000);
            _monitorTimer.Elapsed += (s, e) => CheckStatus();
            _monitorTimer.Start();
        }

        private void CheckStatus()
        {
            foreach (var s in Servers)
            {
                s.CheckRunning();
            }
        }

        [RelayCommand]
        public async Task Refresh()
        {
            IsLoading = true;
            Servers.Clear();
            var list = await Globals.WebAPI.FetchDedicatedServersAsync();
            foreach (var info in list)
            {
                Servers.Add(new ServerItemViewModel(info));
                // Console.WriteLine($"Added {info.Name}");
            }
            IsLoading = false;
        }
    }

    public partial class ServerItemViewModel : ObservableObject
    {
        public ServerInfo Info { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        [NotifyPropertyChangedFor(nameof(CanStart))]
        [NotifyPropertyChangedFor(nameof(CanStop))]
        [NotifyPropertyChangedFor(nameof(CanUninstall))]
        private bool _isRunning;

        public bool CanUninstall => !IsRunning;

        public string Name => Info.Name;
        public string AppId => Info.AppId;
        public string StatusColor => IsRunning ? "#4CAF50" : "#F44336"; // Green / Red

        public bool CanStart => !IsRunning;
        public bool CanStop => IsRunning;

        public ServerItemViewModel(ServerInfo info)
        {
            Info = info;
            CheckRunning();
        }

        public void CheckRunning()
        {
            try {
               bool run = Globals.ProcessManager.IsRunning(Info.AppId);
               if (run != IsRunning) IsRunning = run;
            } catch { } 
        }

        [RelayCommand]
        public void Start()
        {
            if (string.IsNullOrEmpty(Info.InstallPath))
            {
                // MessageBox.Show("Install Path is invalid.");
                return;
            }
            
            // Heuristic for EXE: Look for .exe in folder? 
            // In Python version we had logic to find exe.
            // For now, let's assume specific exe names or search first .exe at root?
            // "v_rising_server.exe" etc.
            // Let's implement a quick FindExe helper in Globals or here.
            
            try 
            {
                string exe = Globals.Executables.FindServerExecutable(Info.InstallPath);
                if (exe == null)
                {
                    // MessageBox.Show("Could not find executable.");
                    return;
                }
                Globals.ProcessManager.StartServer(Info.AppId, exe, Info.LaunchArguments ?? "");
                IsRunning = true;
            } 
            catch (Exception ex)
            {
                // MessageBox.Show($"Start Failed: {ex.Message}");
            }
        }

        [RelayCommand]
        public void Stop()
        {
            Globals.ProcessManager.StopServer(Info.AppId);
            IsRunning = false;
        }

        [RelayCommand]
        public void OpenFolder()
        {
            if (System.IO.Directory.Exists(Info.InstallPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", Info.InstallPath);
            }
        }

        [RelayCommand]
        public async Task Uninstall()
        {
            var confirmed = await Globals.Dialogs.ConfirmAsync(
                "Remove server",
                $"Remove '{Name}' from Steam Server Buddy?\n\nServer files will stay on disk.",
                "Remove");
            if (!confirmed) return;

            try
            {
                await Globals.WebAPI.RemoveCustomServerAsync(Info.AppId);
                if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    await mainVm.ServersVM.RefreshCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Remove server failed for {Name}", ex);
            }
        }
    }
}
