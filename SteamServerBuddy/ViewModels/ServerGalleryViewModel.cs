using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Avalonia.Media.Imaging;
using SteamServerBuddy.Models;

namespace SteamServerBuddy.ViewModels
{
    public partial class ServerGalleryViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ServerCardViewModel> _servers = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private double _cardSize = 200; // Default card size in pixels

        public ServerGalleryViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        }

        public IAsyncRelayCommand RefreshCommand { get; }

        public async Task RefreshAsync()
        {
            IsLoading = true;
            Servers.Clear();

            try
            {
                var serverList = await Globals.WebAPI.FetchDedicatedServersAsync();
                foreach (var server in serverList)
                {
                    var card = new ServerCardViewModel(server);
                    Servers.Add(card);
                    // Start loading image in background
                    _ = card.LoadImageAsync(); 
                }
            }
            catch (Exception ex)
            {
                // Log or show error
                System.Diagnostics.Debug.WriteLine($"Failed to load servers: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public partial class ServerCardViewModel : ObservableObject
    {
        public string Name { get; }
        public string AppId { get; }
        public string InstallPath { get; }
        private readonly ServerInfo _info;

        [ObservableProperty]
        private string _statusMessage = "";

        [ObservableProperty]
        private Bitmap _imageSource;

        [ObservableProperty]
        private Avalonia.Media.Stretch _imageStretch = Avalonia.Media.Stretch.UniformToFill;

        [ObservableProperty]
        private bool _isRunning = false;

        public string SteamType => _info.SteamType;
        public System.Collections.Generic.List<string> Tags => _info.Tags ?? new();

        public ServerCardViewModel(Models.ServerInfo info)
        {
            _info = info;
            Name = info.Name;
            AppId = info.AppId;
            InstallPath = info.InstallPath;

            // default placeholder
            // ImageSource = ...
        }

        public async Task LoadImageAsync()
        {
            var imageUrls = new[]
            {
                _info.HeaderImageUrl,
                _info.CapsuleImageUrl,
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{AppId}/header.jpg"
            }
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var url in imageUrls)
            {
                var bitmap = await LoadBitmapAsync(url);
                if (bitmap != null)
                {
                    ImageSource = bitmap;
                    return;
                }
            }

            // Fallback to Local Executable Icon
            string exePath = null;

            // Find the executable on a background thread (IO heavy)
            await Task.Run(() => 
            {
                try 
                {
                    exePath = FindServerExecutable(InstallPath);
                    System.Diagnostics.Debug.WriteLine($"[IconFallback] AppId={AppId} FoundExe={exePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IconFallback] Error finding exe for {AppId}: {ex.Message}");
                }
            });

            if (!string.IsNullOrEmpty(exePath))
            {
               // Icon extraction removed for cross-platform compatibility
               // TODO: Implement cross-platform icon loading (e.g. from resources or generic icon)
            }
        }

        private string FindServerExecutable(string installPath)
        {
            if (!Directory.Exists(installPath)) return null;

             // 1. Look for obvious exe with "server" in name
            var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories);
            
            // Prioritize standard server names
            var serverExe = exeFiles.FirstOrDefault(f => f.ToLower().Contains("palworld") && f.ToLower().Contains("win64")); // Palworld specific
            if (serverExe != null) return serverExe;

            serverExe = exeFiles.FirstOrDefault(f => f.ToLower().Contains("enshrouded_server")); // Enshrouded specific
            if (serverExe != null) return serverExe;

            serverExe = exeFiles.FirstOrDefault(f => f.ToLower().Contains("valheim_server")); // Valheim specific
            if (serverExe != null) return serverExe;
            
            serverExe = exeFiles.FirstOrDefault(f => f.ToLower().Contains("vrisingserver")); // V Rising specific
            if (serverExe != null) return serverExe;

            // General heuristic
            serverExe = exeFiles.FirstOrDefault(f => f.ToLower().Contains("server") && !f.ToLower().Contains("unity") && !f.ToLower().Contains("crash") && !f.ToLower().Contains("steam"));
            if (serverExe != null) return serverExe;

            // Fallback to any exe that isn't unity/crash/steam
            return exeFiles.FirstOrDefault(f => !f.ToLower().Contains("unity") && !f.ToLower().Contains("crash") && !f.ToLower().Contains("steam"));
        }

        // ExtractIcon removed

        [RelayCommand]
        public async Task OpenDetail()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.DataContext is MainViewModel mainVm)
            {
                var serverItem = new ServerItemViewModel(_info);
                await mainVm.OpenDetailCommand.ExecuteAsync(serverItem);
            }
        }

        private async Task<Bitmap> LoadBitmapAsync(string url)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var bytes = await client.GetByteArrayAsync(url);
                using var stream = new MemoryStream(bytes);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        public void OpenFolder()
        {
            if (!Directory.Exists(InstallPath))
            {
                StatusMessage = "Folder not found.";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = InstallPath,
                    UseShellExecute = true
                });
                StatusMessage = "";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Open failed: {ex.Message}";
            }
        }

        [RelayCommand]
        public async Task Uninstall()
        {
            try
            {
                var confirmed = await Globals.Dialogs.ConfirmAsync(
                    "Remove server",
                    $"Remove '{Name}' from Steam Server Buddy?\n\nServer files will stay on disk.",
                    "Remove");
                if (!confirmed) return;

                StatusMessage = "Removing...";
                await Globals.WebAPI.RemoveCustomServerAsync(AppId);

                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    await mainVm.ServerGalleryVM.RefreshCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Remove failed: {ex.Message}";
                Debug.WriteLine($"Uninstall failed: {ex.Message}");
            }
        }
    }
}
