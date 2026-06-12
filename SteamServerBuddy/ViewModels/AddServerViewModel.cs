using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SteamServerBuddy.ViewModels
{
    public partial class AddServerViewModel : ObservableObject 
    {
        private static readonly HttpClient ImageClient = new();
        private Models.SteamAppMetadata _metadata;

        [ObservableProperty]
        private string _appId = string.Empty;

        [ObservableProperty]
        private string _serverName = string.Empty;

        [ObservableProperty]
        private string _installFolder = string.Empty;

        [ObservableProperty]
        private string _importPath = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private bool _isBusy = false;

        [ObservableProperty]
        private bool _hasMetadata = false;

        [ObservableProperty]
        private Bitmap _headerImage;

        [ObservableProperty]
        private string _steamType = "";

        [ObservableProperty]
        private string _steamDbUrl = "";

        [ObservableProperty]
        private string _steamStoreUrl = "";

        [ObservableProperty]
        private string _steamCmdCommand = "";

        public ObservableCollection<string> Tags { get; } = new();
        public ObservableCollection<string> InstallLog { get; } = new();

        public IAsyncRelayCommand LookupCommand { get; }
        public IAsyncRelayCommand ImportCommand { get; }
        public IAsyncRelayCommand InstallCommand { get; }
        public IAsyncRelayCommand BrowseCommand { get; }
        public IAsyncRelayCommand BrowseInstallFolderCommand { get; }
        public IRelayCommand OpenSteamDbCommand { get; }
        public IRelayCommand OpenSteamStoreCommand { get; }
        public IRelayCommand CopyCommandCommand { get; }

        public AddServerViewModel()
        {
            LookupCommand = new AsyncRelayCommand(LookupAsync);
            ImportCommand = new AsyncRelayCommand(ImportExistingAsync);
            InstallCommand = new AsyncRelayCommand(InstallNewAsync);
            BrowseCommand = new AsyncRelayCommand(BrowseFolderAsync);
            BrowseInstallFolderCommand = new AsyncRelayCommand(BrowseInstallFolderAsync);
            OpenSteamDbCommand = new RelayCommand(() => OpenUrl(SteamDbUrl));
            OpenSteamStoreCommand = new RelayCommand(() => OpenUrl(SteamStoreUrl));
            CopyCommandCommand = new RelayCommand(CopyCommand);
        }

        public async Task PrepareAppAsync(string appId)
        {
            AppId = appId ?? "";
            await LookupAsync();
        }

        partial void OnAppIdChanged(string value)
        {
            HasMetadata = false;
            HeaderImage = null;
            Tags.Clear();
            SteamCmdCommand = "";
            if (!string.IsNullOrWhiteSpace(value) && value.All(char.IsDigit))
            {
                InstallFolder = Path.Combine(Globals.AppSettings.GetServerInstallPath(), value);
            }
        }

        private async Task LookupAsync()
        {
            if (string.IsNullOrWhiteSpace(AppId) || !AppId.All(char.IsDigit))
            {
                StatusMessage = "Error: enter a numeric Steam AppID.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Looking up Steam app details...";
            Tags.Clear();

            try
            {
                _metadata = await Globals.WebAPI.GetAppMetadataAsync(AppId);
                if (_metadata == null)
                {
                    HasMetadata = false;
                    StatusMessage = "No Steam metadata found for that AppID.";
                    return;
                }

                ServerName = _metadata.Name;
                SteamType = _metadata.Type;
                SteamDbUrl = _metadata.SteamDbUrl;
                SteamStoreUrl = _metadata.SteamStoreUrl;
                InstallFolder = Path.Combine(Globals.AppSettings.GetServerInstallPath(), SanitizeFolderName(_metadata.Name, AppId));
                SteamCmdCommand = BuildSteamCmdCommand(AppId, InstallFolder);
                foreach (var tag in _metadata.Tags) Tags.Add(tag);
                HeaderImage = await LoadBitmapAsync(_metadata.HeaderImageUrl);
                HasMetadata = true;
                StatusMessage = "Steam app details loaded.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lookup failed: {ex.Message}";
                Globals.Diagnostics.Error($"Add server lookup failed for {AppId}", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BrowseFolderAsync()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow?.StorageProvider is not { } storageProvider)
            {
                StatusMessage = "Error: Folder picker is unavailable.";
                return;
            }

            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select existing server folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                ImportPath = folders[0].Path.LocalPath;
            }
        }

        private async Task BrowseInstallFolderAsync()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow?.StorageProvider is not { } storageProvider)
            {
                StatusMessage = "Error: Folder picker is unavailable.";
                return;
            }

            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose install folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                InstallFolder = folders[0].Path.LocalPath;
                SteamCmdCommand = BuildSteamCmdCommand(AppId, InstallFolder);
            }
        }

        private async Task ImportExistingAsync()
        {
            if (string.IsNullOrWhiteSpace(ImportPath) || !Directory.Exists(ImportPath))
            {
                StatusMessage = "Error: Invalid folder path.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Importing server...";

            try 
            {
                var customId = $"custom-{DateTime.Now:yyyyMMddHHmmss}";
                await Globals.WebAPI.AddCustomServerAsync(customId, ImportPath);
                StatusMessage = "Success: Server imported!";
                AddLog($"Imported {ImportPath}");
                ImportPath = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally 
            {
                IsBusy = false;
            }
        }

        private async Task InstallNewAsync()
        {
            if (string.IsNullOrWhiteSpace(AppId))
            {
                StatusMessage = "Error: Please enter a Steam AppID.";
                return;
            }

            if (!AppId.All(char.IsDigit))
            {
                StatusMessage = "Error: Steam AppID should contain numbers only.";
                return;
            }

            IsBusy = true;
            StatusMessage = "Starting SteamCMD...";
            InstallProgress = 0;
            InstallLog.Clear();
            AddLog($"Starting install for App ID {AppId}");

            try 
            {
                if (_metadata == null || _metadata.AppId != AppId)
                {
                    await LookupAsync();
                }
                
                var installDir = string.IsNullOrWhiteSpace(InstallFolder)
                    ? Path.Combine(Globals.AppSettings.GetServerInstallPath(), AppId)
                    : InstallFolder;
                AddLog($"Install directory: {installDir}");
                
                var tcs = new TaskCompletionSource<bool>();
                await Globals.SteamCMD.InstallServerAsync(AppId, installDir, status => 
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusMessage = status;
                        AddLog(status);
                        if (status.Contains("progress") || status.Contains("Downloaded")) InstallProgress += 1;
                        if (InstallProgress > 95) InstallProgress = 95;
                    });
                }, 
                success => {
                    tcs.SetResult(success);
                });

                bool finalSuccess = await tcs.Task;
                if (!finalSuccess) throw new Exception("SteamCMD installation failed.");

                var info = new Models.ServerInfo
                {
                    AppId = AppId,
                    Name = string.IsNullOrWhiteSpace(ServerName) ? _metadata?.Name ?? $"App {AppId}" : ServerName,
                    InstallPath = installDir,
                    IsInstalled = true
                };
                Services.SteamWebAPIService.ApplyMetadata(info, _metadata);
                await Globals.WebAPI.AddCustomServerAsync(info);
                
                StatusMessage = $"Success: App {AppId} installed!";
                AddLog(StatusMessage);
                InstallProgress = 100;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                AddLog(StatusMessage);
            }
            finally 
            {
                IsBusy = false;
            }
        }

        private void AddLog(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            InstallLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (InstallLog.Count > 500)
            {
                InstallLog.RemoveAt(0);
            }
        }

        private static string SanitizeFolderName(string name, string fallback)
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            var invalidChars = Path.GetInvalidFileNameChars();
            var folderName = string.Join("_", name.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return string.IsNullOrWhiteSpace(folderName) ? fallback : folderName;
        }

        private static string BuildSteamCmdCommand(string appId, string installFolder)
        {
            return $"steamcmd +force_install_dir \"{installFolder}\" +login anonymous +app_update {appId} validate +quit";
        }

        private static async Task<Bitmap> LoadBitmapAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var bytes = await ImageClient.GetByteArrayAsync(url);
                using var ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private async void CopyCommand()
        {
            if (string.IsNullOrWhiteSpace(SteamCmdCommand)) return;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(SteamCmdCommand);
                StatusMessage = "SteamCMD command copied.";
            }
        }
    }
}
