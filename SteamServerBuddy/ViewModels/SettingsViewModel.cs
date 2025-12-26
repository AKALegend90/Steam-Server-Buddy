using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Models;

namespace SteamServerBuddy.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _serverName;

        [ObservableProperty]
        private string _appId;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private bool _isEmbedded;

        public ObservableCollection<ConfigGroupViewModel> Groups { get; } = new ObservableCollection<ConfigGroupViewModel>();

        private GameSchema _schema;
        private string _serverPath;

        public SettingsViewModel()
        {
            SaveCommand = new AsyncRelayCommand(SaveAsync);
            OpenSettingsFolderCommand = new RelayCommand(OpenSettingsFolder);
            OpenFileLocationCommand = new RelayCommand<ConfigGroupViewModel>(OpenFileLocation);
            OpenInNotepadCommand = new RelayCommand<ConfigGroupViewModel>(OpenInNotepad);
        }

        public IAsyncRelayCommand SaveCommand { get; }
        public IRelayCommand OpenSettingsFolderCommand { get; }
        public IRelayCommand OpenFileLocationCommand { get; }
        public IRelayCommand OpenInNotepadCommand { get; }

        public async Task LoadAsync(string appId, string serverName, string serverPath)
        {
            AppId = appId;
            ServerName = serverName;
            _serverPath = serverPath;
            IsBusy = true;
            StatusMessage = "Looking for configs...";

            _schema = await Globals.Config.LoadSchemaAsync(appId, serverPath);
            if (_schema == null || _schema.ConfigFiles == null || !_schema.ConfigFiles.Any())
            {
                StatusMessage = "No configuration files found.";
                IsBusy = false;
                return;
            }

            StatusMessage = "Loading values...";
            var values = await Globals.Config.LoadSettingsValuesAsync(_schema, _serverPath);

            Groups.Clear();
            foreach (var configFile in _schema.ConfigFiles)
            {
                var group = new ConfigGroupViewModel 
                { 
                    FileName = configFile.Name,
                    FullPath = System.IO.Path.Combine(_serverPath, configFile.Path)
                };
                foreach (var field in configFile.Fields)
                {
                    var uiKey = $"{configFile.Name}_{field.Key}";
                    var currentVal = values.ContainsKey(uiKey) ? values[uiKey] : field.Default;
                    
                    group.Fields.Add(new SettingFieldViewModel(field, currentVal, configFile.Name));
                }
                
                if (group.Fields.Any())
                {
                    Groups.Add(group);
                }
            }

            StatusMessage = "Ready";
            IsBusy = false;
        }

        private async Task SaveAsync()
        {
            if (_schema == null || string.IsNullOrEmpty(_serverPath)) return;

            IsBusy = true;
            StatusMessage = "Saving...";

            var valuesToSave = Groups.SelectMany(g => g.Fields)
                                     .ToDictionary(f => $"{f.ConfigFileName}_{f.Key}", f => f.Value);
                                     
            await Globals.Config.SaveSettingsValuesAsync(_schema, _serverPath, valuesToSave);

            StatusMessage = "Saved!";
            IsBusy = false;
        }

        private void OpenSettingsFolder()
        {
            if (string.IsNullOrEmpty(_serverPath) || !System.IO.Directory.Exists(_serverPath)) return;
            try { System.Diagnostics.Process.Start("explorer.exe", _serverPath); } catch { }
        }

        private void OpenFileLocation(ConfigGroupViewModel group)
        {
            if (group == null || string.IsNullOrEmpty(group.FullPath)) return;
            if (!System.IO.File.Exists(group.FullPath))
            {
                System.Windows.MessageBox.Show(
                    $"The config file does not exist yet:\n{group.FullPath}\n\nPlease start the server at least once to generate this file.",
                    "File Not Found",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }
            try 
            { 
                var cleanPath = System.IO.Path.GetFullPath(group.FullPath);
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{cleanPath}\""); 
            } 
            catch { }
        }

        private void OpenInNotepad(ConfigGroupViewModel group)
        {
            if (group == null || string.IsNullOrEmpty(group.FullPath)) return;
            if (!System.IO.File.Exists(group.FullPath))
            {
                System.Windows.MessageBox.Show(
                    $"The config file does not exist yet:\n{group.FullPath}\n\nPlease start the server at least once to generate this file.",
                    "File Not Found",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }
            try 
            { 
                var cleanPath = System.IO.Path.GetFullPath(group.FullPath);
                System.Diagnostics.Process.Start("notepad.exe", cleanPath); 
            } 
            catch { }
        }
    }

    public partial class ConfigGroupViewModel : ObservableObject
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public ObservableCollection<SettingFieldViewModel> Fields { get; } = new ObservableCollection<SettingFieldViewModel>();
    }

    public partial class SettingFieldViewModel : ObservableObject
    {
        public string Key { get; }
        public string Label { get; }
        public string Type { get; }
        public string Category { get; }
        public string Description { get; }
        public List<OptionDefinition> Options { get; }
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }
        public string ConfigFileName { get; }

        [ObservableProperty]
        private object _value;

        public SettingFieldViewModel(FieldDefinition def, object currentValue, string configFileName)
        {
            Key = def.Key;
            Label = def.Label;
            Type = def.Type?.ToLower() ?? "text";
            Category = def.Category;
            Description = def.Description;
            Min = def.Min ?? 0;
            Max = def.Max ?? 1000;
            Step = def.Step ?? 1;
            ConfigFileName = configFileName;

            if (def.Options != null)
            {
                Options = new List<OptionDefinition>();
                foreach (var opt in def.Options)
                {
                    if (opt is string s) 
                    {
                        Options.Add(new OptionDefinition { Label = s, Value = s });
                    }
                    else if (opt is OptionDefinition od)
                    {
                        Options.Add(od);
                    }
                    else if (opt is Newtonsoft.Json.Linq.JObject jo) 
                    {
                        var optDef = jo.ToObject<OptionDefinition>();
                        if (optDef != null) Options.Add(optDef);
                    }
                    else if (opt is Newtonsoft.Json.Linq.JToken token)
                    {
                        if (token.Type == Newtonsoft.Json.Linq.JTokenType.String)
                            Options.Add(new OptionDefinition { Label = token.ToString(), Value = token.ToString() });
                        else if (token.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                        {
                            var optDef = token.ToObject<OptionDefinition>();
                            if (optDef != null) Options.Add(optDef);
                        }
                    }
                }
            }

            // Handle Value Conversion
            if (Type == "toggle")
            {
                var strVal = currentValue?.ToString()?.ToLower();
                _value = (strVal == "true" || strVal == "on" || strVal == "1");
            }
            else if (Type == "number")
            {
                if (double.TryParse(currentValue?.ToString(), out var d)) _value = d;
                else _value = 0.0;
            }
            else if (Type == "select" && Options != null && Options.Count > 0)
            {
                var currentStr = currentValue?.ToString()?.Trim();
                
                // Try case-insensitive matching for both Label and Value
                var match = Options.FirstOrDefault(o => 
                    string.Equals(o.Value?.ToString(), currentStr, StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(o.Label?.ToString(), currentStr, StringComparison.OrdinalIgnoreCase));
                
                if (match != null)
                {
                    _value = match.Value;
                }
                else
                {
                    // If no match found, use first option or null to avoid ComboBox selection crash
                    // WPF ComboBox with SelectedValuePath will crash if bound value type mismatches Item values
                    _value = Options.FirstOrDefault()?.Value;
                }
            }
            else
            {
                _value = currentValue;
            }
        }
    }
}
