using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows.Input;

namespace SteamServerBuddy.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object _currentView;

        public ServersViewModel ServersVM { get; } = new ServersViewModel();
        public ServerGalleryViewModel ServerGalleryVM { get; } = new ServerGalleryViewModel();
        public AddServerViewModel AddServerVM { get; } = new AddServerViewModel();
        public AppSettingsViewModel SettingsVM { get; } = new AppSettingsViewModel();
        public ConsoleViewModel ConsoleVM { get; } = new ConsoleViewModel();
        public ServerDetailViewModel ServerDetailVM { get; } = new ServerDetailViewModel();
        public DashboardViewModel DashboardVM { get; }

        public MainViewModel()
        {
            DashboardVM = new DashboardViewModel(ServersVM);
            CurrentView = DashboardVM;
        }

        [RelayCommand]
        public void Navigate(string viewName)
        {
            switch (viewName)
            {
                case "Dashboard": 
                    _ = DashboardVM.RefreshAsync();
                    CurrentView = DashboardVM; 
                    break;
                case "Servers":
                    _ = ServerGalleryVM.RefreshAsync();
                    CurrentView = ServerGalleryVM;
                    break;
                case "Add": CurrentView = AddServerVM; break;
                case "Settings": CurrentView = SettingsVM; break;
                case "Console": CurrentView = ConsoleVM; break;
            }
        }

        [RelayCommand]
        public void OpenConsole(ServerItemViewModel server)
        {
            var logPath = Path.Combine(server.Info.InstallPath, "server.log");
            ConsoleVM.Load(server.Name, logPath);
            CurrentView = ConsoleVM;
        }

        // Remove OpenSettings command since we're not using per-server settings anymore

        [RelayCommand]
        public async Task OpenDetail(ServerItemViewModel server)
        {
            await ServerDetailVM.LoadAsync(server);
            CurrentView = ServerDetailVM;
        }
    }
}
