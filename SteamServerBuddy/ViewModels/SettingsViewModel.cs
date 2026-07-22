using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
            OpenFileLocationCommand = new AsyncRelayCommand<ConfigGroupViewModel>(OpenFileLocationAsync);
            OpenInNotepadCommand = new AsyncRelayCommand<ConfigGroupViewModel>(OpenInNotepadAsync);
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
            Groups.Clear();

            _schema = await Globals.Config.LoadSchemaAsync(appId, serverPath);
            if (_schema == null || _schema.ConfigFiles == null || !_schema.ConfigFiles.Any())
            {
                StatusMessage = "No configuration files found.";
                IsBusy = false;
                return;
            }

            if (IsVRisingApp(appId))
            {
                PrepareVRisingOverrideFiles(serverPath);
            }

            StatusMessage = "Loading values...";
            var values = await Globals.Config.LoadSettingsValuesAsync(_schema, _serverPath);

            Groups.Clear();
            foreach (var configFile in _schema.ConfigFiles)
            {
                var group = new ConfigGroupViewModel 
                { 
                    FileName = configFile.Name,
                    FullPath = System.IO.Path.Combine(_serverPath, configFile.Path),
                    IsPalworld = appId == "2394010",
                    IsVRising = IsVRisingApp(appId)
                };
                foreach (var field in configFile.Fields)
                {
                    var uiKey = $"{configFile.Name}_{field.Key}";
                    var currentVal = values.ContainsKey(uiKey) ? values[uiKey] : field.Default;
                    // An earlier slider-based port editor could coerce large port values to 100
                    // before its Maximum binding was applied. Restore known Palworld defaults in that case.
                    if (appId == "2394010" && (field.Key is "PublicPort" or "RESTAPIPort" or "RCONPort") &&
                        double.TryParse(currentVal?.ToString(), out var loadedPort) && loadedPort == 100)
                    {
                        currentVal = field.Default;
                    }
                    
                    group.Fields.Add(new SettingFieldViewModel(field, currentVal, configFile.Name, group.IsCategorized));
                }
                
                if (group.Fields.Any())
                {
                    if (group.IsCategorized)
                    {
                        var categories = group.IsPalworld
                            ? PalworldCategories
                            : GetVRisingCategories(configFile.Name);
                        foreach (var category in categories)
                        {
                            var categoryGroup = new SettingCategoryViewModel { Name = category };
                            foreach (var setting in group.Fields.Where(f => GetCategory(group, configFile.Name, f.Key) == category)
                                                                 .OrderBy(f => group.IsPalworld ? GetPalworldSortOrder(category, f.Key) : int.MaxValue))
                            {
                                if (setting.IsToggleInput) categoryGroup.Toggles.Add(setting);
                                else categoryGroup.Fields.Add(setting);
                            }
                            if (categoryGroup.Fields.Any() || categoryGroup.Toggles.Any()) group.Categories.Add(categoryGroup);
                        }
                    }
                    Groups.Add(group);
                }
            }

            StatusMessage = "Ready";
            IsBusy = false;
        }

        private static readonly string[] PalworldCategories =
            { "Admin", "Gameplay", "Game Balance", "Performance", "Undocumented" };

        private static readonly string[] VRisingHostCategories =
            { "Server Identity", "Network & Listing", "Access & Security", "Saves & Resets", "Performance & Admin" };

        private static readonly string[] VRisingGameCategories =
            { "World & Difficulty", "PvP & Raiding", "Progression & Combat", "Loot & Economy", "Castle & Building", "Time & Events" };

        private static bool IsVRisingApp(string appId) => appId is "1829350" or "1604030";

        private static string[] GetVRisingCategories(string fileName) =>
            fileName.Equals("ServerHostSettings.json", StringComparison.OrdinalIgnoreCase)
                ? VRisingHostCategories
                : VRisingGameCategories;

        private static string GetCategory(ConfigGroupViewModel group, string fileName, string key)
        {
            if (group.IsPalworld) return GetPalworldCategory(key);
            return fileName.Equals("ServerHostSettings.json", StringComparison.OrdinalIgnoreCase)
                ? GetVRisingHostCategory(key)
                : GetVRisingGameCategory(key);
        }

        private static string GetVRisingHostCategory(string key)
        {
            if (key is "Name" or "Description" or "GameSettingsPreset" or "GameDifficultyPreset") return "Server Identity";
            if (key is "Port" or "QueryPort" or "Address" or "HideIPAddress" or "ListOnSteam" or "ListOnEOS" or "LanMode") return "Network & Listing";
            if (key is "MaxConnectedUsers" or "MaxConnectedAdmins" or "Password" or "Secure" or "SafeReconnectTime" or "SafeReconnectSlots") return "Access & Security";
            if (key is "SaveName" or "AutoSaveCount" or "AutoSaveInterval" or "AutoSaveSmartKeep" or "CompressSaveFiles" or "ResetDaysInterval" or "DayOfReset") return "Saves & Resets";
            return "Performance & Admin";
        }

        private static string GetVRisingGameCategory(string key)
        {
            if (key.StartsWith("CastleStatModifiers", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Castle", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Build", StringComparison.OrdinalIgnoreCase)) return "Castle & Building";
            if (key.StartsWith("GameTimeModifiers", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("PlayerInteractionSettings", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("WarEvent", StringComparison.OrdinalIgnoreCase)) return "Time & Events";
            if (key.Contains("Drop", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Yield", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Inventory", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Trader", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Resource", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Refinement", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Research", StringComparison.OrdinalIgnoreCase)) return "Loot & Economy";
            if (key.Contains("PvP", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("PlayerDamage", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Siege", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Relic", StringComparison.OrdinalIgnoreCase)) return "PvP & Raiding";
            if (key.StartsWith("Vampire", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("UnitStat", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("EquipmentStat", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Starter", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Damage", StringComparison.OrdinalIgnoreCase) ||
                key.Contains("Durability", StringComparison.OrdinalIgnoreCase)) return "Progression & Combat";
            return "World & Difficulty";
        }

        private static void PrepareVRisingOverrideFiles(string serverPath)
        {
            if (string.IsNullOrWhiteSpace(serverPath) || !Directory.Exists(serverPath)) return;

            var sourceDirectory = Path.Combine(serverPath, "VRisingServer_Data", "StreamingAssets", "Settings");
            var targetDirectory = Path.Combine(serverPath, "save-data", "Settings");
            Directory.CreateDirectory(targetDirectory);

            foreach (var fileName in new[] { "ServerHostSettings.json", "ServerGameSettings.json" })
            {
                var source = Path.Combine(sourceDirectory, fileName);
                var target = Path.Combine(targetDirectory, fileName);
                if (!File.Exists(target) && File.Exists(source)) File.Copy(source, target);
            }
        }

        private static string GetPalworldCategory(string key)
        {
            if (key is "ServerName" or "ServerDescription" or "AdminPassword" or "ServerPassword" or "PublicIP" or "PublicPort" or
                "ServerPlayerMaxNum" or "RCONEnabled" or "RCONPort" or "RESTAPIEnabled" or "RESTAPIPort" or "CrossplayPlatforms" or
                "AllowConnectPlatform" or "LogFormatType" or "bIsUseBackupSaveData" or "AutoSaveSpan" or "ChatPostLimitPerMinute" or
                "bAllowClientMod" or "bIsShowJoinLeftMessage" or "bEnableBuildingPlayerUIdDisplay" or "Region" or "bUseAuth" or "BanListURL")
                return "Admin";

            if (key is "BaseCampMaxNum" or "BaseCampMaxNumInGuild" or "BaseCampWorkerMaxNum" or "MaxBuildingLimitNum" or
                "PhysicsActiveDropItemMaxNum" or "ServerReplicatePawnCullDistance" or "ItemContainerForceMarkDirtyInterval" or
                "DropItemMaxNum_UNKO" or "MaxGuildsPerFrame" or "PlayerDataPalStorageUpdateCheckTickInterval" or
                "AutoTransferMasterCheckIntervalSeconds" or "AutoTransferMasterThresholdDays" or "BuildingNameDisplayCacheTTLSeconds")
                return "Performance";

            if (key is "Difficulty" or "DayTimeSpeedRate" or "NightTimeSpeedRate" or "ExpRate" or "PalCaptureRate" or "PalSpawnNumRate" or
                "PalDamageRateAttack" or "PalDamageRateDefense" or "PlayerDamageRateAttack" or "PlayerDamageRateDefense" or
                "PlayerStomachDecreaceRate" or "PlayerStaminaDecreaceRate" or "PlayerAutoHPRegeneRate" or "PlayerAutoHpRegeneRateInSleep" or
                "PalStomachDecreaceRate" or "PalStaminaDecreaceRate" or "PalAutoHPRegeneRate" or "PalAutoHpRegeneRateInSleep" or
                "BuildObjectDamageRate" or "BuildObjectDeteriorationDamageRate" or "CollectionDropRate" or "CollectionObjectHpRate" or
                "CollectionObjectRespawnSpeedRate" or "EnemyDropItemRate" or "EquipmentDurabilityDamageRate" or "ItemWeightRate" or
                "ItemCorruptionMultiplier" or "MonsterFarmActionSpeedRate" or "PalEggDefaultHatchingTime" or "SupplyDropSpan" or
                "DeathPenalty" or "GuildPlayerMaxNum" or "GuildRejoinCooldownMinutes" or "BlockRespawnTime" or
                "RespawnPenaltyDurationThreshold" or "RespawnPenaltyTimeScale")
                return "Game Balance";

            if (key is "WorkSpeedRate" or "BuildObjectHpRate" or "DropItemMaxNum" or "DropItemAliveMaxHours" or
                "bEnablePlayerToPlayerDamage" or "bEnableFriendlyFire" or "bActiveUNKO" or "bEnableAimAssistPad" or
                "bEnableAimAssistKeyboard" or "bIsMultiplay" or "bCanPickupOtherGuildDeathPenaltyDrop" or "bEnableNonLoginPenalty" or
                "bEnableDefenseOtherGuildPlayer" or "CoopPlayerMaxNum" or "EnablePredatorBossPal")
                return "Undocumented";

            return "Gameplay";
        }

        private static int GetPalworldSortOrder(string category, string key)
        {
            if (category != "Admin") return int.MaxValue;
            string[] adminOrder =
            {
                "ServerName", "ServerDescription", "ServerPassword", "AdminPassword", "ServerPlayerMaxNum",
                "RESTAPIEnabled", "RESTAPIPort", "RCONEnabled", "RCONPort", "bIsUseBackupSaveData",
                "AutoSaveSpan", "bIsShowJoinLeftMessage", "bAllowClientMod", "ChatPostLimitPerMinute",
                "LogFormatType", "CrossplayPlatforms", "PublicIP", "PublicPort",
                "bEnableBuildingPlayerUIdDisplay", "AllowConnectPlatform", "Region", "bUseAuth", "BanListURL"
            };
            var index = Array.IndexOf(adminOrder, key);
            return index >= 0 ? index : int.MaxValue;
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
            if (string.IsNullOrEmpty(_serverPath))
            {
                StatusMessage = "No server folder is loaded.";
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(_serverPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _serverPath,
                    UseShellExecute = true
                });
                StatusMessage = "Opened server folder.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Open folder failed: {ex.Message}";
            }
        }

        private async Task OpenFileLocationAsync(ConfigGroupViewModel group)
        {
            if (group == null || string.IsNullOrEmpty(group.FullPath)) return;
            if (!await EnsureConfigFileExistsAsync(group)) return;

            try 
            { 
                var cleanPath = System.IO.Path.GetFullPath(group.FullPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{cleanPath}\"",
                    UseShellExecute = true
                });
                StatusMessage = $"Showing {group.FileName}.";
            } 
            catch (Exception ex)
            {
                StatusMessage = $"Show file failed: {ex.Message}";
            }
        }

        private async Task OpenInNotepadAsync(ConfigGroupViewModel group)
        {
            if (group == null || string.IsNullOrEmpty(group.FullPath)) return;
            if (!await EnsureConfigFileExistsAsync(group)) return;

            try 
            { 
                var cleanPath = System.IO.Path.GetFullPath(group.FullPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{cleanPath}\"",
                    UseShellExecute = true
                });
                StatusMessage = $"Opened {group.FileName}.";
            } 
            catch (Exception ex)
            {
                StatusMessage = $"Edit raw failed: {ex.Message}";
            }
        }

        private async Task<bool> EnsureConfigFileExistsAsync(ConfigGroupViewModel group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.FullPath)) return false;

            try
            {
                if (!System.IO.File.Exists(group.FullPath))
                {
                    StatusMessage = $"Creating {group.FileName}...";
                    await SaveAsync();
                }

                if (!System.IO.File.Exists(group.FullPath))
                {
                    StatusMessage = $"Could not create {group.FileName}. Try Save Settings first.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not prepare {group.FileName}: {ex.Message}";
                return false;
            }
        }
    }

    public partial class ConfigGroupViewModel : ObservableObject
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public bool IsPalworld { get; set; }
        public bool IsVRising { get; set; }
        public bool IsCategorized => IsPalworld || IsVRising;
        public bool IsStandard => !IsCategorized;
        public ObservableCollection<SettingFieldViewModel> Fields { get; } = new ObservableCollection<SettingFieldViewModel>();
        public ObservableCollection<SettingCategoryViewModel> Categories { get; } = new ObservableCollection<SettingCategoryViewModel>();
    }

    public class SettingCategoryViewModel
    {
        public string Name { get; set; }
        public ObservableCollection<SettingFieldViewModel> Fields { get; } = new ObservableCollection<SettingFieldViewModel>();
        public ObservableCollection<SettingFieldViewModel> Toggles { get; } = new ObservableCollection<SettingFieldViewModel>();
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

        [ObservableProperty]
        private double _sliderValue;

        partial void OnSliderValueChanged(double value)
        {
            if (IsSliderNumber) Value = value;
        }

        partial void OnValueChanged(object value)
        {
            if (IsSliderNumber && double.TryParse(value?.ToString(), out var number) && SliderValue != number)
                SliderValue = number;
        }

        [ObservableProperty]
        private OptionDefinition _selectedOption;

        partial void OnSelectedOptionChanged(OptionDefinition value)
        {
            if (value != null) Value = value.Value;
        }

        public bool UseSlider { get; }
        public bool IsSliderNumber => UseSlider && Type == "number" && !IsPlainNumber;
        public bool IsPlainNumber => Type == "number" &&
            (Key.EndsWith("Port", StringComparison.OrdinalIgnoreCase) ||
             Key is "PublicPort" or "ServerPlayerMaxNum" or "CoopPlayerMaxNum" or "AutoSaveSpan" or
             "ChatPostLimitPerMinute" or "GuildPlayerMaxNum" or "BaseCampMaxNum" or
             "BaseCampMaxNumInGuild" or "BaseCampWorkerMaxNum" or "MaxBuildingLimitNum");
        public bool IsTextInput => UseSlider && Type == "text";
        public bool IsSelectInput => UseSlider && Type == "select";
        public bool IsToggleInput => UseSlider && Type is "toggle" or "boolean";
        public bool IsFallbackInput => !IsSliderNumber && !IsTextInput && !IsSelectInput && !IsToggleInput;

        public SettingFieldViewModel(FieldDefinition def, object currentValue, string configFileName, bool usePalworldControls = false)
        {
            Key = def.Key;
            Label = def.Label;
            Type = def.Type?.ToLower() ?? "text";
            Category = def.Category;
            Description = def.Description;
            var configuredDefault = double.TryParse((currentValue ?? def.Default)?.ToString(), out var numericDefault)
                ? numericDefault
                : 0;
            Min = def.Min ?? Math.Min(0, configuredDefault);
            Max = def.Max ?? Math.Max(1000, configuredDefault);
            Step = def.Step ?? 1;
            ConfigFileName = configFileName;
            UseSlider = usePalworldControls;

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
                if (double.TryParse(currentValue?.ToString(), out var d))
                {
                    _value = d;
                    _sliderValue = d;
                }
                else
                {
                    _value = 0.0;
                    _sliderValue = 0.0;
                }
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
                    _selectedOption = match;
                }
                else
                {
                    // If no match found, use first option or null to avoid ComboBox selection crash
                    // WPF ComboBox with SelectedValuePath will crash if bound value type mismatches Item values
                    _value = Options.FirstOrDefault()?.Value;
                    _selectedOption = Options.FirstOrDefault();
                }
            }
            else
            {
                _value = currentValue;
            }
        }
    }
}
