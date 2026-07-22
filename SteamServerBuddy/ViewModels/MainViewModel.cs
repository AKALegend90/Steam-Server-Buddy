using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Services;
using System.IO;


namespace SteamServerBuddy.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private object _currentView;

        public ServersViewModel ServersVM { get; } = new ServersViewModel();
        public ServerGalleryViewModel ServerGalleryVM { get; } = new ServerGalleryViewModel();
        public AddServerViewModel AddServerVM { get; } = new AddServerViewModel();
        public ServerCatalogViewModel CatalogVM { get; } = new ServerCatalogViewModel();
        public AppSettingsViewModel SettingsVM { get; } = new AppSettingsViewModel();
        public HelpViewModel HelpVM { get; } = new HelpViewModel();
        public ConsoleViewModel ConsoleVM { get; } = new ConsoleViewModel();
        public ServerDetailViewModel ServerDetailVM { get; } = new ServerDetailViewModel();
        public DashboardViewModel DashboardVM { get; }
        public string QuickThemeButtonText => SettingsVM.Theme == AppThemeService.LightTheme
            ? "☾  Switch to Dark"
            : "☀  Switch to Light";

        public MainViewModel()
        {
            DashboardVM = new DashboardViewModel(ServersVM);
            CurrentView = DashboardVM;
            SettingsVM.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SettingsVM.Theme))
                    OnPropertyChanged(nameof(QuickThemeButtonText));
            };
        }

        [RelayCommand]
        public void ToggleTheme()
        {
            SettingsVM.Theme = SettingsVM.Theme == AppThemeService.LightTheme
                ? AppThemeService.DarkTheme
                : AppThemeService.LightTheme;
            OnPropertyChanged(nameof(QuickThemeButtonText));
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
                case "Catalog":
                    _ = CatalogVM.RefreshAsync();
                    CurrentView = CatalogVM;
                    break;
                case "Settings": CurrentView = SettingsVM; break;
                case "Console": CurrentView = ConsoleVM; break;
                case "Help": CurrentView = HelpVM; break;
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
