using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Models;
using SteamServerBuddy.Services;

namespace SteamServerBuddy.ViewModels
{
    public partial class ServerCatalogViewModel : ObservableObject
    {
        private readonly ObservableCollection<ServerCatalogItemViewModel> _allItems = new();

        public ObservableCollection<ServerCatalogItemViewModel> Items { get; } = new();

        [ObservableProperty] private string _searchText = "";
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage = "Search the built-in dedicated server catalog by name or AppID.";
        [ObservableProperty] private int _resultCount;

        public ServerCatalogViewModel()
        {
            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        }

        public IAsyncRelayCommand RefreshCommand { get; }

        partial void OnSearchTextChanged(string value)
        {
            ApplyFilter();
        }

        public async Task RefreshAsync()
        {
            IsLoading = true;
            StatusMessage = "Loading Steam dedicated server catalog...";

            try
            {
                var catalog = await Globals.WebAPI.FetchDedicatedServerCatalogAsync();
                _allItems.Clear();
                foreach (var item in catalog)
                {
                    _allItems.Add(new ServerCatalogItemViewModel(item, InstallItemAsync));
                }

                ApplyFilter();
                StatusMessage = $"Loaded {_allItems.Count} built-in dedicated server entries.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Catalog load failed: {ex.Message}";
                Globals.Diagnostics.Error("Server catalog load failed", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? "";
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allItems
                : new ObservableCollection<ServerCatalogItemViewModel>(_allItems.Where(item =>
                    item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.AppId.Contains(query, StringComparison.OrdinalIgnoreCase)));

            Items.Clear();
            foreach (var item in filtered.Take(500))
            {
                Items.Add(item);
            }

            ResultCount = filtered.Count;
        }

        private static async Task InstallItemAsync(SteamDedicatedServerCatalogItem item)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.DataContext is MainViewModel mainVm)
            {
                await mainVm.AddServerVM.PrepareAppAsync(item.AppId);
                mainVm.CurrentView = mainVm.AddServerVM;
            }
        }
    }

    public partial class ServerCatalogItemViewModel : ObservableObject
    {
        private readonly SteamDedicatedServerCatalogItem _item;
        private readonly Func<SteamDedicatedServerCatalogItem, Task> _install;

        public ServerCatalogItemViewModel(SteamDedicatedServerCatalogItem item, Func<SteamDedicatedServerCatalogItem, Task> install)
        {
            _item = item;
            _install = install;
        }

        public string AppId => _item.AppId;
        public string Name => _item.Name;
        public string Source => _item.Source;

        [RelayCommand]
        public async Task Install()
        {
            await _install(_item);
        }
    }
}
