using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Net.Http;

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

        [ObservableProperty]
        private object _imageSource; // Can be string URL or ImageSource object

        [ObservableProperty]
        private System.Windows.Media.Stretch _imageStretch = System.Windows.Media.Stretch.UniformToFill;

        [ObservableProperty]
        private bool _isRunning = false;

        public ServerCardViewModel(Models.ServerInfo info)
        {
            Name = info.Name;
            AppId = info.AppId;
            InstallPath = info.InstallPath;

            // default placeholder
            // ImageSource = ...
        }

        public async Task LoadImageAsync()
        {
            // 1. Try Steam CDN (Game Header)
            string url = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{AppId}/header.jpg";
            bool valid = await CheckUrlExists(url);

            if (valid)
            {
                ImageSource = url;
            }
            else
            {
                // 2. Fallback to Local Executable Icon
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
                    // Extract icon and create BitmapSource on UI thread to ensure thread affinity
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        var iconBitmap = ExtractIcon(exePath);
                        if (iconBitmap != null)
                        {
                            ImageSource = iconBitmap;
                            ImageStretch = System.Windows.Media.Stretch.Uniform;
                        }
                    });
                }
            }
        }

        private async Task<bool> CheckUrlExists(string url)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(2);
                    var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
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

        private ImageSource ExtractIcon(string path)
        {
            try
            {
                using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                {
                    if (icon == null) return null;
                    
                    return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        public async Task OpenDetail()
        {
            // Navigate to detail view
            if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
            {
                // Create a temporary ServerItemViewModel from the stored ServerInfo
                var serverList = await Globals.WebAPI.FetchDedicatedServersAsync();
                var matchingServer = serverList.FirstOrDefault(s => s.AppId == AppId);
                
                if (matchingServer != null)
                {
                    var serverItem = new ServerItemViewModel(matchingServer);
                    await mainVm.OpenDetailCommand.ExecuteAsync(serverItem);
                }
            }
        }

        [RelayCommand]
        public void OpenFolder()
        {
            if (System.IO.Directory.Exists(InstallPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", InstallPath);
            }
        }

        [RelayCommand]
        public async Task Uninstall()
        {
            var result = System.Windows.MessageBox.Show(
                $"Do you want to delete the server files for {Name}?\n\nYes = Delete files and remove from list\nNo = Only remove from list (keep files)\nCancel = Don't uninstall",
                "Confirm Uninstall",
                System.Windows.MessageBoxButton.YesNoCancel);

            if (result == System.Windows.MessageBoxResult.Cancel) return;

            try
            {
                // Remove from custom_servers.json via WebAPI
                await Globals.WebAPI.RemoveCustomServerAsync(AppId);

                // Delete files if requested
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    if (System.IO.Directory.Exists(InstallPath))
                    {
                        // Stop the server first if running (simple check via ProcessManager)
                        if (Globals.ProcessManager.IsRunning(AppId))
                        {
                            Globals.ProcessManager.StopServer(AppId);
                            await Task.Delay(1000); // Wait for process to stop
                        }

                        try
                        {
                            System.IO.Directory.Delete(InstallPath, true);
                            System.Windows.MessageBox.Show($"Server '{Name}' has been uninstalled and files deleted.", "Success");
                        }
                        catch (Exception ex)
                        {
                            System.Windows.MessageBox.Show($"Server removed from list, but failed to delete files:\n{ex.Message}\n\nYou may need to delete manually.", "Warning");
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"Server '{Name}' removed from list. Files not found.", "Info");
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show($"Server '{Name}' removed from list. Files kept at:\n{InstallPath}", "Info");
                }

                // Trigger refresh on the parent view model (ServerGalleryViewModel)
                if (System.Windows.Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    await mainVm.ServerGalleryVM.RefreshCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Uninstall failed: {ex.Message}", "Error");
            }
        }
    }
}
