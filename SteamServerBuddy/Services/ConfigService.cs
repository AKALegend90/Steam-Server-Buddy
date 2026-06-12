using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamServerBuddy.Models;

namespace SteamServerBuddy.Services
{
    public class ConfigService
    {
        private readonly string _configsDir;

        private static readonly Dictionary<string, string> SchemaAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // Palworld game app -> Palworld Dedicated Server app/config schema.
            ["1623730"] = "2394010"
        };

        private static readonly Dictionary<string, List<OptionDefinition>> NativeEnumRegistry = new(StringComparer.OrdinalIgnoreCase)
        {
            ["GameDifficulty"] = new() { 
                new() { Label = "Relaxed", Value = 0 }, 
                new() { Label = "Normal", Value = 1 }, 
                new() { Label = "Hard (Brutal)", Value = 2 } 
            },
            ["Difficulty"] = new() { 
                new() { Label = "Relaxed", Value = 0 }, 
                new() { Label = "Normal", Value = 1 }, 
                new() { Label = "Hard (Brutal)", Value = 2 } 
            },
            ["GameModeType"] = new() { 
                new() { Label = "PvE", Value = 0 }, 
                new() { Label = "PvP", Value = 1 } 
            },
            ["CastleDamageMode"] = new() { 
                new() { Label = "Never", Value = 0 }, 
                new() { Label = "Always", Value = 1 }, 
                new() { Label = "TimeRestricted", Value = 2 } 
            },
            ["PlayerDamageMode"] = new() { 
                new() { Label = "Always", Value = 0 }, 
                new() { Label = "TimeRestricted", Value = 1 } 
            },
            ["SiegeWeaponHealth"] = new() {
                new() { Label = "VeryLow (750HP)", Value = 0 },
                new() { Label = "Low (1000HP)", Value = 1 },
                new() { Label = "Normal (1250HP)", Value = 2 },
                new() { Label = "High (1750HP)", Value = 3 },
                new() { Label = "VeryHigh (2500HP)", Value = 4 },
                new() { Label = "MegaHigh (3250HP)", Value = 5 },
                new() { Label = "UltraHigh (4000HP)", Value = 6 },
                new() { Label = "CrazyHigh (5000HP)", Value = 7 },
                new() { Label = "Max (7500HP)", Value = 8 }
            },
            ["CastleHeartDamageMode"] = new() { 
                new() { Label = "CanBeDestroyedOnlyWhenDecaying", Value = "CanBeDestroyedOnlyWhenDecaying" },
                new() { Label = "CanBeDestroyedByPlayers", Value = "CanBeDestroyedByPlayers" },
                new() { Label = "CanBeSeizedOrDestroyedByPlayers", Value = "CanBeSeizedOrDestroyedByPlayers" } 
            },
            ["PvPProtectionMode"] = new() { 
                new() { Label = "Disabled (No protection)", Value = "Disabled" }, 
                new() { Label = "VeryShort (900s)", Value = "VeryShort" }, 
                new() { Label = "Short (1800s)", Value = "Short" }, 
                new() { Label = "Medium (3600s)", Value = "Medium" }, 
                new() { Label = "Long (7200s)", Value = "Long" } 
            },
            ["DeathContainerPermission"] = new() { 
                new() { Label = "Anyone", Value = "Anyone" }, 
                new() { Label = "ClanMembers", Value = "ClanMembers" }, 
                new() { Label = "OnlySelf", Value = "OnlySelf" } 
            },
            ["RelicSpawnType"] = new() { 
                new() { Label = "Unique (One Shard per Type)", Value = "Unique" }, 
                new() { Label = "Plentiful (No shard limit)", Value = "Plentiful" } 
            },
            ["TimeZone"] = new() { 
                new() { Label = "Local", Value = "Local" }, 
                new() { Label = "UTC", Value = "UTC" }, 
                new() { Label = "EST", Value = "EST" } 
            }
        };

        public ConfigService()
        {
            _configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
            if (!Directory.Exists(_configsDir)) Directory.CreateDirectory(_configsDir);
        }

        public async Task<GameSchema> LoadSchemaAsync(string appId, string serverPath = null)
        {
            var schemaAppId = ResolveSchemaAppId(appId);
            var path = Path.Combine(_configsDir, $"{schemaAppId}.json");
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonConvert.DeserializeObject<GameSchema>(json);
            }

            // Fallback: Try name-based lookup
            var namePath = Path.Combine(_configsDir, $"{schemaAppId.ToLower()}.json");
            if (File.Exists(namePath))
            {
                var json = await File.ReadAllTextAsync(namePath);
                return JsonConvert.DeserializeObject<GameSchema>(json);
            }

            // If no schema for AppID, try to auto-discover in the server path
            if (!string.IsNullOrEmpty(serverPath))
            {
                return await DiscoverConfigsAsync(serverPath);
            }

            return null;
        }

        public async Task<GameSchema> DiscoverConfigsAsync(string serverPath)
        {
            return await Task.Run(async () =>
            {
                var schema = new GameSchema { 
                    ConfigFiles = new List<ConfigFileDefinition>() 
                };

                if (!Directory.Exists(serverPath)) return schema;

                var extensions = new[] { "*.ini", "*.json", "*.cfg", "*.txt" };
                var allFiles = new List<string>();
                
                foreach (var ext in extensions)
                {
                    allFiles.AddRange(Directory.GetFiles(serverPath, ext, SearchOption.AllDirectories));
                }

                // Filter for relevant server/game settings keywords
                var relevantKeywords = new[] { "server", "host", "game", "config", "settings" };
                var filteredFiles = allFiles.Where(f => {
                    var name = Path.GetFileName(f).ToLower();
                    // Exclude common junk
                    if (name.Contains("steam") || name.Contains("unity") || name.Contains("redist") || name.Contains("crash")) return false;
                    return relevantKeywords.Any(k => name.Contains(k));
                }).ToList();

                // Limit to top 20 relevant files 
                foreach (var file in filteredFiles.OrderBy(f => f.Length).Take(20))
                {
                    var relPath = Path.GetRelativePath(serverPath, file);
                    var format = Path.GetExtension(file).TrimStart('.').ToLower();
                    if (string.IsNullOrEmpty(format)) format = "text";

                    var configDef = new ConfigFileDefinition
                    {
                        Name = Path.GetFileName(file),
                        Path = relPath,
                        Format = format,
                        Fields = new List<FieldDefinition>()
                    };

                    try
                    {
                        if (format == "json")
                        {
                            var content = await File.ReadAllTextAsync(file);
                            var obj = JObject.Parse(content);
                            foreach (var prop in obj.Properties())
                            {
                                var val = prop.Value.ToString();
                                var type = "text";
                                if (prop.Value.Type == JTokenType.Boolean) type = "toggle";
                                else if (prop.Value.Type == JTokenType.Integer || prop.Value.Type == JTokenType.Float) type = "number";
                                else if (bool.TryParse(val, out _)) type = "toggle";

                                var field = new FieldDefinition { 
                                    Key = prop.Name, 
                                    Label = prop.Name, 
                                    Type = type,
                                    Default = val
                                };

                                if (NativeEnumRegistry.ContainsKey(prop.Name))
                                {
                                    field.Type = "select";
                                    field.Options = NativeEnumRegistry[prop.Name].Cast<object>().ToList();
                                }

                                configDef.Fields.Add(field);
                            }
                        }
                        else if (format == "ini" || format == "cfg" || format == "text")
                        {
                            var lines = await File.ReadAllLinesAsync(file);
                            foreach (var line in lines)
                            {
                                if (line.Contains('=') && !line.Trim().StartsWith(";") && !line.Trim().StartsWith("#"))
                                {
                                    var parts = line.Split('=', 2);
                                    var key = parts[0].Trim();
                                    var val = parts[1].Trim().ToLower();

                                    if (!string.IsNullOrEmpty(key) && !configDef.Fields.Any(f => f.Key == key))
                                    {
                                        var type = "text";
                                        if (val == "true" || val == "false" || val == "on" || val == "off" || val == "1" || val == "0") 
                                        {
                                            if (val == "1" || val == "0") type = "number"; 
                                            else type = "toggle";
                                        }
                                        else if (double.TryParse(val, out _)) type = "number";

                                        var field = new FieldDefinition { 
                                            Key = key, 
                                            Label = key, 
                                            Type = type 
                                        };

                                        if (NativeEnumRegistry.ContainsKey(key))
                                        {
                                            field.Type = "select";
                                            field.Options = NativeEnumRegistry[key].Cast<object>().ToList();
                                        }

                                        configDef.Fields.Add(field);
                                    }
                                }
                            }
                        }
                    }
                    catch { /* Skip unparseable files */ }

                    if (configDef.Fields.Any())
                    {
                        schema.ConfigFiles.Add(configDef);
                    }
                }

                return schema;
            });
        }

        public async Task<Dictionary<string, object>> LoadSettingsValuesAsync(GameSchema schema, string serverPath)
        {
            var values = new Dictionary<string, object>();
            if (schema == null || string.IsNullOrEmpty(serverPath)) return values;

            foreach (var configFile in schema.ConfigFiles)
            {
                var fullPath = Path.Combine(serverPath, configFile.Path);
                if (!File.Exists(fullPath)) continue;

                try
                {
                    if (configFile.Format.ToLower() == "json")
                    {
                        var json = await File.ReadAllTextAsync(fullPath);
                        var obj = JObject.Parse(json);
                        foreach (var field in configFile.Fields)
                        {
                            var token = obj.SelectToken(field.Key);
                            if (token != null)
                            {
                                values[$"{configFile.Name}_{field.Key}"] = token.ToObject<object>();
                            }
                        }
                    }
                    else if (IsKeyValueFormat(configFile.Format))
                    {
                        var lines = await File.ReadAllLinesAsync(fullPath);
                        foreach (var field in configFile.Fields)
                        {
                            // Simple INI key search (doesn't support sections yet, but most game configs are simple)
                            var line = lines.FirstOrDefault(l => l.Contains('=') && l.Split('=')[0].Trim().Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                            if (line != null)
                            {
                                var parts = line.Split('=', 2);
                                values[$"{configFile.Name}_{field.Key}"] = parts[1].Trim();
                            }
                        }
                    }
                    else if (configFile.Format.ToLower() == "palworld")
                    {
                        var lines = await File.ReadAllLinesAsync(fullPath);
                        var settingsLine = lines.FirstOrDefault(l => l.StartsWith("OptionSettings=("));
                        if (settingsLine != null)
                        {
                            var palValues = ParsePalworldSettings(settingsLine);
                            foreach (var field in configFile.Fields)
                            {
                                if (palValues.ContainsKey(field.Key))
                                {
                                    values[$"{configFile.Name}_{field.Key}"] = palValues[field.Key];
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading config {configFile.Name}: {ex.Message}");
                }
            }

            return values;
        }

        public async Task SaveSettingsValuesAsync(GameSchema schema, string serverPath, Dictionary<string, object> newValues)
        {
            if (schema == null || string.IsNullOrEmpty(serverPath)) return;

            foreach (var configFile in schema.ConfigFiles)
            {
                var fullPath = Path.Combine(serverPath, configFile.Path);
                
                try
                {
                    if (configFile.Format.ToLower() == "json")
                    {
                        JObject obj;
                        if (File.Exists(fullPath))
                        {
                            var json = await File.ReadAllTextAsync(fullPath);
                            obj = JObject.Parse(json);
                        }
                        else
                        {
                            obj = new JObject();
                            var dir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        }

                        bool sourceChanged = false;
                        foreach (var field in configFile.Fields)
                        {
                            var uiKey = $"{configFile.Name}_{field.Key}";
                            if (newValues.ContainsKey(uiKey))
                            {
                                var parts = field.Key.Split('.');
                                JContainer current = obj;
                                for (int i = 0; i < parts.Length - 1; i++)
                                {
                                    if (current[parts[i]] == null)
                                    {
                                        current[parts[i]] = new JObject();
                                    }
                                    current = (JContainer)current[parts[i]];
                                }
                                current[parts[parts.Length - 1]] = JToken.FromObject(newValues[uiKey]);
                                sourceChanged = true;
                            }
                        }

                        if (sourceChanged)
                        {
                            await File.WriteAllTextAsync(fullPath, obj.ToString(Formatting.Indented));
                        }
                    }
                    else if (IsKeyValueFormat(configFile.Format))
                    {
                        List<string> lines = File.Exists(fullPath) 
                            ? (await File.ReadAllLinesAsync(fullPath)).ToList() 
                            : new List<string>();

                        bool sourceChanged = false;
                        foreach (var field in configFile.Fields)
                        {
                            var uiKey = $"{configFile.Name}_{field.Key}";
                            if (newValues.ContainsKey(uiKey))
                            {
                                var newVal = newValues[uiKey]?.ToString() ?? "";
                                var lineIndex = lines.FindIndex(l => l.Contains('=') && l.Split('=')[0].Trim().Equals(field.Key, StringComparison.OrdinalIgnoreCase));
                                
                                if (lineIndex >= 0)
                                {
                                    lines[lineIndex] = $"{field.Key}={newVal}";
                                }
                                else
                                {
                                    lines.Add($"{field.Key}={newVal}");
                                }
                                sourceChanged = true;
                            }
                        }

                        if (sourceChanged)
                        {
                            var dir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            await File.WriteAllLinesAsync(fullPath, lines);
                        }
                    }
                    else if (configFile.Format.ToLower() == "palworld")
                    {
                        List<string> lines = File.Exists(fullPath) 
                            ? (await File.ReadAllLinesAsync(fullPath)).ToList() 
                            : new List<string>();

                        var lineIndex = lines.FindIndex(l => l.StartsWith("OptionSettings=("));
                        var settingsLine = lineIndex >= 0 ? lines[lineIndex] : "OptionSettings=(Difficulty=None)";
                        
                        var palValues = ParsePalworldSettings(settingsLine);
                        bool changed = false;

                        foreach (var field in configFile.Fields)
                        {
                            var uiKey = $"{configFile.Name}_{field.Key}";
                            if (newValues.ContainsKey(uiKey))
                            {
                                var newVal = newValues[uiKey]?.ToString() ?? "";
                                palValues[field.Key] = newVal;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            var newSettingsLine = UpdatePalworldSettings(settingsLine, palValues);
                            if (lineIndex >= 0) lines[lineIndex] = newSettingsLine;
                            else 
                            {
                                if (!lines.Any(l => l.Contains("[/Script/Pal.PalGameWorldSettings]")))
                                    lines.Add("[/Script/Pal.PalGameWorldSettings]");
                                lines.Add(newSettingsLine);
                            }

                            var dir = Path.GetDirectoryName(fullPath);
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            await File.WriteAllLinesAsync(fullPath, lines);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving config {configFile.Name}: {ex.Message}");
                }
            }
        }

        public async Task<int?> DetectServerPortAsync(string appId, string serverPath)
        {
            if (string.IsNullOrWhiteSpace(serverPath) || !Directory.Exists(serverPath)) return GetKnownDefaultPort(appId);

            var schema = await LoadSchemaAsync(appId, serverPath);
            var values = schema != null ? await LoadSettingsValuesAsync(schema, serverPath) : new Dictionary<string, object>();
            var detected = FindPortInValues(values);
            if (detected.HasValue) return detected;

            detected = await FindPortInConfigFilesAsync(serverPath);
            if (detected.HasValue) return detected;

            return GetKnownDefaultPort(appId);
        }

        private static int? FindPortInValues(Dictionary<string, object> values)
        {
            var preferredKeys = new[]
            {
                "port",
                "serverport",
                "server_port",
                "gameport",
                "game_port",
                "queryport",
                "query_port"
            };

            foreach (var preferred in preferredKeys)
            {
                var match = values.FirstOrDefault(v => IsPortKey(v.Key, preferred));
                if (!string.IsNullOrEmpty(match.Key) && TryReadPort(match.Value, out var port)) return port;
            }

            foreach (var value in values)
            {
                if (LooksLikePortKey(value.Key) && TryReadPort(value.Value, out var port)) return port;
            }

            return null;
        }

        private static string ResolveSchemaAppId(string appId)
        {
            return !string.IsNullOrWhiteSpace(appId) && SchemaAliases.TryGetValue(appId, out var alias)
                ? alias
                : appId;
        }

        private async Task<int?> FindPortInConfigFilesAsync(string serverPath)
        {
            var extensions = new[] { "*.ini", "*.cfg", "*.txt", "*.json" };
            var files = new List<string>();

            foreach (var extension in extensions)
            {
                files.AddRange(Directory.GetFiles(serverPath, extension, SearchOption.AllDirectories));
            }

            foreach (var file in files.Where(IsLikelyConfigFile).OrderBy(f => f.Length).Take(40))
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var detected = Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase)
                        ? FindPortInJson(content)
                        : FindPortInText(content);

                    if (detected.HasValue) return detected;
                }
                catch
                {
                    // Ignore unreadable or malformed config files during discovery.
                }
            }

            return null;
        }

        private static int? FindPortInJson(string content)
        {
            try
            {
                var token = JToken.Parse(content);
                return FindPortInJsonToken(token);
            }
            catch
            {
                return null;
            }
        }

        private static int? FindPortInJsonToken(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    if (LooksLikePortKey(prop.Name) && TryReadPort(prop.Value.ToString(), out var port)) return port;

                    var nested = FindPortInJsonToken(prop.Value);
                    if (nested.HasValue) return nested;
                }
            }
            else if (token is JArray arr)
            {
                foreach (var child in arr)
                {
                    var nested = FindPortInJsonToken(child);
                    if (nested.HasValue) return nested;
                }
            }

            return null;
        }

        private static int? FindPortInText(string content)
        {
            var matches = Regex.Matches(content, @"(?im)^\s*[""']?(?<key>[A-Za-z0-9_.-]*port[A-Za-z0-9_.-]*)[""']?\s*[:=]\s*[""']?(?<value>\d{2,5})[""']?");
            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value;
                var value = match.Groups["value"].Value;
                if (LooksLikePortKey(key) && TryReadPort(value, out var port)) return port;
            }

            return null;
        }

        private static bool IsLikelyConfigFile(string path)
        {
            var lower = path.ToLowerInvariant();
            if (lower.Contains("\\steamapps\\") || lower.Contains("\\_commonredist\\")) return false;
            if (lower.Contains("crash") || lower.Contains("log") || lower.Contains("backup")) return false;
            return true;
        }

        private static bool IsPortKey(string key, string expected)
        {
            var normalized = NormalizePortKey(key);
            return normalized.Equals(expected.Replace("_", ""), StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikePortKey(string key)
        {
            var normalized = NormalizePortKey(key);
            return normalized.Contains("port", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("viewport", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("teleport", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("airport", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePortKey(string key)
        {
            var shortKey = key.Split('_').LastOrDefault() ?? key;
            return Regex.Replace(shortKey, @"[^A-Za-z0-9]", "").ToLowerInvariant();
        }

        private static bool TryReadPort(object value, out int port)
        {
            port = 0;
            if (!int.TryParse(value?.ToString(), out var parsed)) return false;
            if (parsed < 1 || parsed > 65535) return false;

            port = parsed;
            return true;
        }

        private static int? GetKnownDefaultPort(string appId)
        {
            return appId switch
            {
                "2394010" => 8211,  // Palworld
                "1203620" => 15636, // Enshrouded
                "892970" => 2456,   // Valheim
                "1604030" => 9876,  // V Rising
                "4019830" => 7777,  // RuneScape Dragonwilds: Dedicated Server
                "1374490" => 7777,  // RuneScape Dragonwilds
                _ => null
            };
        }

        private Dictionary<string, string> ParsePalworldSettings(string line)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var start = line.IndexOf('(');
            var end = line.LastIndexOf(')');
            if (start < 0 || end < 0) return values;

            var content = line.Substring(start + 1, end - start - 1);
            
            // Robust parsing for comma-separated values respecting quotes
            var currentToken = "";
            bool inQuote = false;
            
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (c == '"')
                {
                    inQuote = !inQuote;
                    currentToken += c;
                }
                else if (c == ',' && !inQuote)
                {
                    ProcessToken(values, currentToken);
                    currentToken = "";
                }
                else
                {
                    currentToken += c;
                }
            }
            ProcessToken(values, currentToken); // Last token
            
            return values;
        }

        private void ProcessToken(Dictionary<string, string> values, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            var part = token.Trim();
            if (part.Contains('='))
            {
                var kv = part.Split('=', 2);
                // We keep the value as-is for now (including quotes) to preserve original formatting preference?
                // Or we trim them to standardize? The Update method re-adds quotes if missing.
                // Let's trim them to have a clean "raw" value.
                values[kv[0].Trim()] = kv[1].Trim().Trim('"');
            }
        }

        private static bool IsKeyValueFormat(string format)
        {
            return format.Equals("ini", StringComparison.OrdinalIgnoreCase) ||
                   format.Equals("cfg", StringComparison.OrdinalIgnoreCase) ||
                   format.Equals("txt", StringComparison.OrdinalIgnoreCase) ||
                   format.Equals("text", StringComparison.OrdinalIgnoreCase);
        }

        private string UpdatePalworldSettings(string originalLine, Dictionary<string, string> values)
        {
            // We want to preserve the order if possible, or just rebuild from dictionary
            // Palworld is very picky about the format. It's best to join them back.
            var settings = string.Join(",", values.Select(kv => 
            {
                var val = kv.Value;
                // Palworld requires quotes for string values
                // We check if the value is boolean or numeric; if not, wrap in quotes
                bool isBool = val.Equals("True", StringComparison.OrdinalIgnoreCase) || val.Equals("False", StringComparison.OrdinalIgnoreCase);
                bool isNumber = double.TryParse(val, out _);
                
                // Special case for None/Difficulty enums which are not quoted
                bool isReserverKeyword = val.Equals("None", StringComparison.OrdinalIgnoreCase) || val.Equals("All", StringComparison.OrdinalIgnoreCase);

                if (!isBool && !isNumber && !isReserverKeyword && !val.StartsWith("\""))
                {
                    val = $"\"{val}\"";
                }
                
                return $"{kv.Key}={val}";
            }));
            return $"OptionSettings=({settings})";
        }
    }
}
