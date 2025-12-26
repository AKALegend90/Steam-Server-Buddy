using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SteamServerBuddy.ViewModels
{
    public partial class AddServerViewModel : ObservableObject 
    {
        [ObservableProperty]
        private string _appId = string.Empty;

        [ObservableProperty]
        private string _importPath = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private double _installProgress = 0;

        [ObservableProperty]
        private bool _isBusy = false;

        public IAsyncRelayCommand ImportCommand { get; }
        public IAsyncRelayCommand InstallCommand { get; }
        public IRelayCommand BrowseCommand { get; }

        public AddServerViewModel()
        {
            ImportCommand = new AsyncRelayCommand(ImportExistingAsync);
            InstallCommand = new AsyncRelayCommand(InstallNewAsync);
            BrowseCommand = new RelayCommand(BrowseFolder);
        }

        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                ImportPath = dialog.FolderName;
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
                await Globals.WebAPI.AddCustomServerAsync("0", ImportPath);
                StatusMessage = "Success: Server imported!";
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

            IsBusy = true;
            StatusMessage = "Starting SteamCMD...";
            InstallProgress = 0;

            try 
            {
                StatusMessage = "Resolving game name...";
                var gameName = await Globals.WebAPI.GetAppNameAsync(AppId);
                
                string folderName = AppId;
                if (!string.IsNullOrEmpty(gameName))
                {
                    // Sanitize path
                    var invalidChars = Path.GetInvalidFileNameChars();
                    folderName = string.Join("_", gameName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
                }

                var installDir = Path.Combine(Globals.AppSettings.GetServerInstallPath(), folderName);
                
                var tcs = new TaskCompletionSource<bool>();
                await Globals.SteamCMD.InstallServerAsync(AppId, installDir, status => 
                {
                    StatusMessage = status;
                    if (status.Contains("progress") || status.Contains("Downloaded")) InstallProgress += 1; 
                    if (InstallProgress > 95) InstallProgress = 95;
                }, 
                success => {
                    tcs.SetResult(success);
                });

                bool finalSuccess = await tcs.Task;
                if (!finalSuccess) throw new Exception("SteamCMD installation failed.");

                await Globals.WebAPI.AddCustomServerAsync(AppId, installDir);
                
                StatusMessage = $"Success: App {AppId} installed!";
                InstallProgress = 100;
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
    }
}
