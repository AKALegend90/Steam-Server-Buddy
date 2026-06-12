using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SteamServerBuddy.Models;

namespace SteamServerBuddy.Services
{
    public class SteamWebAPIService
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly Dictionary<string, string> ArtworkAppAliases = new()
        {
            ["294420"] = "251570",   // 7 Days to Die
            ["2278520"] = "1203620", // Enshrouded
            ["2394010"] = "1623730", // Palworld
            ["4019830"] = "1374490", // RuneScape Dragonwilds
            ["1690800"] = "526870",  // Satisfactory
            ["896660"] = "892970",   // Valheim
            ["1829350"] = "1604030", // V Rising
            ["258550"] = "252490"    // Rust
        };
        
        public SteamWebAPIService()
        {
            AppPaths.EnsureDataDirectories();
            MigrateLegacyFile(FindLegacyCustomServersPath(), CustomServersPath);
            MigrateLegacyFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "manifests", "user_paths.json"), UserPathsPath);
        }

        public string CustomServersPath => AppPaths.CustomServersPath;
        public string UserPathsPath => AppPaths.UserPathsPath;

        private static void MigrateLegacyFile(string? oldPath, string newPath)
        {
            if (string.IsNullOrWhiteSpace(oldPath) || !File.Exists(oldPath) || File.Exists(newPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Copy(oldPath, newPath);
        }

        private static string? FindLegacyCustomServersPath()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "custom_servers.json");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }

            return null;
        }

        public async Task<List<ServerInfo>> FetchDedicatedServersAsync()
        {
            var results = new List<ServerInfo>();

            // 1. Load Custom Servers
            if (File.Exists(CustomServersPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(CustomServersPath);
                    var customServers = JsonConvert.DeserializeObject<List<ServerInfo>>(json);
                    if (customServers != null)
                    {
                        results.AddRange(customServers);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading custom servers: {ex.Message}");
                }
            }

            // 2. Load Installed Paths
            if (File.Exists(UserPathsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(UserPathsPath);
                    var userPaths = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    
                    if (userPaths != null)
                    {
                        foreach (var kvp in userPaths)
                        {
                            var appid = kvp.Key;
                            var path = kvp.Value;

                            // Skip if already exists
                            if (results.Any(s => s.AppId == appid.ToString())) continue;

                            // Better fallback: Priority: 1. Exe File Name, 2. Steam API Name, 3. Folder Name
                            var name = FindServerName(path);

                            // 2. Fetch Name from API if exe name not found or just to be sure (though user wants exe name)
                            // If user specifically wants EXE name, maybe we should skip Steam API?
                            // User said: "I want to use the name of the .exe file as name, not the the app id"
                            // If I found an EXE, I'll use it.
                            if (string.IsNullOrEmpty(name) || name.Contains("Server")) // If fallback name or generic
                            {
                                try
                                {
                                    var response = await _client.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appid}");
                                    var data = JObject.Parse(response);
                                    string appKey = appid.ToString();
                                    
                                    if (data[appKey] != null && data[appKey]["success"]?.Value<bool>() == true)
                                    {
                                        name = data[appKey]["data"]["name"].ToString();
                                    }
                                }
                                catch { }
                            }

                            // 3. Last fallback: Folder name
                            if (string.IsNullOrEmpty(name))
                            {
                                var folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                                name = !string.IsNullOrEmpty(folderName) ? folderName : $"Server {appid}";
                            }

                            results.Add(new ServerInfo
                            {
                                AppId = appid,
                                Name = name,
                                InstallPath = path,
                                IsInstalled = true
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading user paths: {ex.Message}");
                }
            }

            await RepairMissingMetadataAsync(results);
            return results;
        }
        public async Task AddCustomServerAsync(string appId, string installPath)
        {
            var servers = new List<ServerInfo>();
            if (File.Exists(CustomServersPath))
            {
                var json = await File.ReadAllTextAsync(CustomServersPath);
                servers = JsonConvert.DeserializeObject<List<ServerInfo>>(json) ?? new List<ServerInfo>();
            }

            var metadata = await GetAppMetadataAsync(appId);
            var name = metadata?.Name;

            // Priority: 1. Steam metadata, 2. Exe/folder fallback
            if (string.IsNullOrWhiteSpace(name))
            {
                name = FindServerName(installPath);
            }

            // 2. Steam API
            if (string.IsNullOrEmpty(name) || name.Contains("Server"))
            {
                try
                {
                    name = await GetAppNameAsync(appId);
                }
                catch { }
            }

            // 3. Folder Name fallback
            if (string.IsNullOrEmpty(name))
            {
                var folderName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                name = !string.IsNullOrEmpty(folderName) ? folderName : $"Server {appId}";
            }

            var info = new ServerInfo
            {
                AppId = appId,
                Name = name,
                InstallPath = installPath,
                IsInstalled = true
            };

            ApplyMetadata(info, metadata);
            servers.Add(info);

            var outputJson = JsonConvert.SerializeObject(servers, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(CustomServersPath)!);
            await File.WriteAllTextAsync(CustomServersPath, outputJson);
        }

        public async Task AddCustomServerAsync(ServerInfo info)
        {
            var servers = new List<ServerInfo>();
            if (File.Exists(CustomServersPath))
            {
                var json = await File.ReadAllTextAsync(CustomServersPath);
                servers = JsonConvert.DeserializeObject<List<ServerInfo>>(json) ?? new List<ServerInfo>();
            }

            var index = servers.FindIndex(s => s.AppId == info.AppId);
            if (index >= 0) servers[index] = info;
            else servers.Add(info);

            Directory.CreateDirectory(Path.GetDirectoryName(CustomServersPath)!);
            await File.WriteAllTextAsync(CustomServersPath, JsonConvert.SerializeObject(servers, Formatting.Indented));
        }

        public async Task<SteamAppMetadata?> GetAppMetadataAsync(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId) || !appId.All(char.IsDigit)) return null;

            try
            {
                var response = await _client.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}");
                var root = JObject.Parse(response);
                var app = root[appId];
                if (app?["success"]?.Value<bool>() != true) return BuildFallbackMetadata(appId);

                var data = app["data"];
                if (data == null) return BuildFallbackMetadata(appId);
                var artworkAppId = ResolveArtworkAppId(appId);

                var tags = new List<string>();
                foreach (var genre in data["genres"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var desc = genre["description"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(desc)) tags.Add(desc);
                }

                foreach (var category in data["categories"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var desc = category["description"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(desc)) tags.Add(desc);
                }

                return new SteamAppMetadata
                {
                    AppId = appId,
                    Name = data["name"]?.ToString() ?? $"App {appId}",
                    Type = data["type"]?.ToString() ?? "",
                    HeaderImageUrl = data["header_image"]?.ToString() ?? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{artworkAppId}/header.jpg",
                    CapsuleImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{artworkAppId}/capsule_616x353.jpg",
                    SteamDbUrl = $"https://steamdb.info/app/{appId}/",
                    SteamStoreUrl = $"https://store.steampowered.com/app/{appId}/",
                    Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList()
                };
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Steam metadata lookup failed for {appId}", ex);
                return BuildFallbackMetadata(appId);
            }
        }

        public async Task<List<SteamDedicatedServerCatalogItem>> FetchDedicatedServerCatalogAsync()
        {
            await Task.CompletedTask;
            return GetKnownDedicatedServers()
                .OrderBy(item => item.Name)
                .ToList();
        }

        private static List<SteamDedicatedServerCatalogItem> GetKnownDedicatedServers()
        {
            return new List<SteamDedicatedServerCatalogItem>
            {
                new() { AppId = "294420", Name = "7 Days to Die Dedicated Server" },
                new() { AppId = "376030", Name = "ARK: Survival Evolved Dedicated Server" },
                new() { AppId = "445400", Name = "ARK: Survival of the Fittest Dedicated Server" },
                new() { AppId = "2430930", Name = "ARK: Survival Ascended Dedicated Server" },
                new() { AppId = "33905", Name = "ARMA 2 Dedicated Server" },
                new() { AppId = "33935", Name = "ARMA 2: Operation Arrowhead Dedicated Server" },
                new() { AppId = "233780", Name = "Arma 3 Dedicated Server" },
                new() { AppId = "346680", Name = "Black Mesa: Deathmatch Dedicated Server" },
                new() { AppId = "228780", Name = "Blade Symphony Dedicated Server" },
                new() { AppId = "332850", Name = "BlazeRush Dedicated Server" },
                new() { AppId = "346330", Name = "BrainBread 2 Dedicated Server" },
                new() { AppId = "72780", Name = "Brink Dedicated Server" },
                new() { AppId = "42750", Name = "Call of Duty: Modern Warfare 3 Dedicated Server" },
                new() { AppId = "667230", Name = "Capsa Dedicated Server" },
                new() { AppId = "258680", Name = "Chivalry: Deadliest Warrior Dedicated Server" },
                new() { AppId = "220070", Name = "Chivalry: Medieval Warfare Dedicated Server" },
                new() { AppId = "443030", Name = "Conan Exiles Dedicated Server" },
                new() { AppId = "238430", Name = "Contagion Dedicated Server" },
                new() { AppId = "90", Name = "Counter-Strike 1.6 Dedicated Server" },
                new() { AppId = "90", Name = "Counter-Strike: Condition Zero Dedicated Server" },
                new() { AppId = "740", Name = "Counter-Strike: Global Offensive Dedicated Server" },
                new() { AppId = "730", Name = "Counter-Strike 2 Dedicated Server" },
                new() { AppId = "232330", Name = "Counter-Strike: Source Dedicated Server" },
                new() { AppId = "343050", Name = "Don't Starve Together Dedicated Server" },
                new() { AppId = "2278520", Name = "Enshrouded Dedicated Server" },
                new() { AppId = "295230", Name = "Fistful of Frags Dedicated Server" },
                new() { AppId = "2915550", Name = "FOUNDRY Dedicated Server" },
                new() { AppId = "4020", Name = "Garry's Mod Dedicated Server" },
                new() { AppId = "5", Name = "Half-Life Dedicated Server" },
                new() { AppId = "232370", Name = "Half-Life 2: Deathmatch Dedicated Server" },
                new() { AppId = "255470", Name = "Half-Life Deathmatch: Source Dedicated Server" },
                new() { AppId = "90", Name = "Half-Life: Opposing Force Dedicated Server" },
                new() { AppId = "55280", Name = "Homefront Dedicated Server" },
                new() { AppId = "405100", Name = "Hurtworld Dedicated Server" },
                new() { AppId = "237410", Name = "Insurgency Dedicated Server" },
                new() { AppId = "17705", Name = "Insurgency: Modern Infantry Combat Dedicated Server" },
                new() { AppId = "581330", Name = "Insurgency: Sandstorm Dedicated Server" },
                new() { AppId = "2181210", Name = "JBMod Dedicated Server" },
                new() { AppId = "261140", Name = "Just Cause 2: Multiplayer Dedicated Server" },
                new() { AppId = "1273", Name = "Killing Floor Beta Dedicated Server" },
                new() { AppId = "215350", Name = "Killing Floor Dedicated Server" },
                new() { AppId = "232130", Name = "Killing Floor 2 Dedicated Server" },
                new() { AppId = "222860", Name = "Left 4 Dead 2 Dedicated Server" },
                new() { AppId = "3796810", Name = "Nightingale Dedicated Server" },
                new() { AppId = "2394010", Name = "Palworld Dedicated Server" },
                new() { AppId = "17575", Name = "Pirates, Vikings, and Knights II Dedicated Server" },
                new() { AppId = "4019830", Name = "RuneScape Dragonwilds: Dedicated Server" },
                new() { AppId = "258550", Name = "Rust Dedicated Server" },
                new() { AppId = "1690800", Name = "Satisfactory Dedicated Server" },
                new() { AppId = "41080", Name = "Serious Sam 3 Dedicated Server" },
                new() { AppId = "403240", Name = "Squad Dedicated Server" },
                new() { AppId = "211820", Name = "Starbound Dedicated Server" },
                new() { AppId = "205", Name = "Source Dedicated Server" },
                new() { AppId = "310", Name = "Source 2007 Dedicated Server" },
                new() { AppId = "244310", Name = "Source SDK Base 2013 Dedicated Server" },
                new() { AppId = "276060", Name = "Sven Co-op Dedicated Server" },
                new() { AppId = "232250", Name = "Team Fortress 2 Dedicated Server" },
                new() { AppId = "90", Name = "Team Fortress Classic Dedicated Server" },
                new() { AppId = "105600", Name = "Terraria Dedicated Server" },
                new() { AppId = "2403", Name = "The Ship Dedicated Server" },
                new() { AppId = "556450", Name = "The Forest Dedicated Server" },
                new() { AppId = "439660", Name = "Tower Unite Dedicated Server" },
                new() { AppId = "1110390", Name = "Unturned Dedicated Server" },
                new() { AppId = "896660", Name = "Valheim Dedicated Server" },
                new() { AppId = "1829350", Name = "V Rising Dedicated Server" },
                new() { AppId = "17505", Name = "Zombie Panic! Source Dedicated Server" }
            };
        }

        private async Task RepairMissingMetadataAsync(List<ServerInfo> servers)
        {
            foreach (var server in servers)
            {
                if (server == null || string.IsNullOrWhiteSpace(server.AppId)) continue;
                if (!NeedsMetadataRepair(server)) continue;

                var before = JsonConvert.SerializeObject(server);
                var metadata = await GetAppMetadataAsync(server.AppId);
                ApplyMetadata(server, metadata);

                if (string.IsNullOrWhiteSpace(server.Name) || IsFallbackName(server.Name, server.AppId))
                {
                    server.Name = GetKnownDedicatedServerName(server.AppId) ?? $"App {server.AppId}";
                }

                if (string.IsNullOrWhiteSpace(server.HeaderImageUrl))
                {
                    var artworkAppId = ResolveArtworkAppId(server.AppId);
                    server.HeaderImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{artworkAppId}/header.jpg";
                    server.CapsuleImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{artworkAppId}/capsule_616x353.jpg";
                }

                var after = JsonConvert.SerializeObject(server);
                if (!string.Equals(before, after, StringComparison.Ordinal))
                {
                    await UpdateServerInfoAsync(server);
                }
            }
        }

        private static bool NeedsMetadataRepair(ServerInfo server)
        {
            return string.IsNullOrWhiteSpace(server.Name)
                || IsFallbackName(server.Name, server.AppId)
                || string.IsNullOrWhiteSpace(server.HeaderImageUrl)
                || string.IsNullOrWhiteSpace(server.SteamDbUrl);
        }

        private static bool IsFallbackName(string name, string appId)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            return name.Equals($"App {appId}", StringComparison.OrdinalIgnoreCase)
                || name.Equals($"AppID: {appId}", StringComparison.OrdinalIgnoreCase)
                || name.Equals($"Server {appId}", StringComparison.OrdinalIgnoreCase)
                || name.Equals(appId, StringComparison.OrdinalIgnoreCase);
        }

        private static SteamAppMetadata BuildFallbackMetadata(string appId)
        {
            var artworkAppId = ResolveArtworkAppId(appId);
            return new SteamAppMetadata
            {
                AppId = appId,
                Name = GetKnownDedicatedServerName(appId) ?? $"App {appId}",
                Type = "game",
                HeaderImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{artworkAppId}/header.jpg",
                CapsuleImageUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{artworkAppId}/capsule_616x353.jpg",
                SteamDbUrl = $"https://steamdb.info/app/{appId}/",
                SteamStoreUrl = $"https://store.steampowered.com/app/{artworkAppId}/",
                Tags = new List<string> { "Dedicated Server" }
            };
        }

        private static string ResolveArtworkAppId(string appId)
        {
            return ArtworkAppAliases.TryGetValue(appId, out var artworkAppId) ? artworkAppId : appId;
        }

        private static string? GetKnownDedicatedServerName(string appId)
        {
            return GetKnownDedicatedServers()
                .FirstOrDefault(server => server.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase))
                ?.Name;
        }

        public static void ApplyMetadata(ServerInfo info, SteamAppMetadata? metadata)
        {
            if (metadata == null) return;

            info.Name = string.IsNullOrWhiteSpace(metadata.Name) ? info.Name : metadata.Name;
            info.SteamType = metadata.Type;
            info.HeaderImageUrl = metadata.HeaderImageUrl;
            info.CapsuleImageUrl = metadata.CapsuleImageUrl;
            info.SteamDbUrl = metadata.SteamDbUrl;
            info.SteamStoreUrl = metadata.SteamStoreUrl;
            info.Tags = metadata.Tags;
        }

        public async Task RemoveCustomServerAsync(string appId)
        {
            // Case 1: Custom Servers (Added via SteamCMD/New)
            if (File.Exists(CustomServersPath))
            {
                var json = await File.ReadAllTextAsync(CustomServersPath);
                var servers = JsonConvert.DeserializeObject<List<ServerInfo>>(json) ?? new List<ServerInfo>();

                var serverToRemove = servers.FirstOrDefault(s => s.AppId == appId);
                if (serverToRemove != null)
                {
                    servers.Remove(serverToRemove);
                    var outputJson = JsonConvert.SerializeObject(servers, Formatting.Indented);
                    Directory.CreateDirectory(Path.GetDirectoryName(CustomServersPath)!);
                    await File.WriteAllTextAsync(CustomServersPath, outputJson);
                    return; // Found and removed
                }
            }
            
            // Case 2: User Paths (Imported Folders)
            await RemoveUserPathAsync(appId);
        }

        private async Task RemoveUserPathAsync(string appId)
        {
            if (!File.Exists(UserPathsPath)) return;

            try 
            {
                var json = await File.ReadAllTextAsync(UserPathsPath);
                var userPaths = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                
                if (userPaths != null && userPaths.ContainsKey(appId))
                {
                    userPaths.Remove(appId);
                    var outputJson = JsonConvert.SerializeObject(userPaths, Formatting.Indented);
                    Directory.CreateDirectory(Path.GetDirectoryName(UserPathsPath)!);
                    await File.WriteAllTextAsync(UserPathsPath, outputJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing user path: {ex.Message}");
            }
        }

        public async Task UpdateServerInfoAsync(ServerInfo info)
        {
            var servers = new List<ServerInfo>();
            if (File.Exists(CustomServersPath))
            {
                var json = await File.ReadAllTextAsync(CustomServersPath);
                servers = JsonConvert.DeserializeObject<List<ServerInfo>>(json) ?? new List<ServerInfo>();
            }

            var index = servers.FindIndex(s => s.AppId == info.AppId);
            if (index >= 0)
            {
                servers[index] = info;
            }
            else
            {
                // New or Promoted from UserPaths
                servers.Add(info);
            }
            
            Directory.CreateDirectory(Path.GetDirectoryName(CustomServersPath)!);
            await File.WriteAllTextAsync(CustomServersPath, JsonConvert.SerializeObject(servers, Formatting.Indented));
        }

        private string FindServerName(string installPath)
        {
            if (!Directory.Exists(installPath)) return null;
            try
            {
                // 1. Look for obvious exe with "server" in name in ROOT
                var rootExes = Directory.GetFiles(installPath, "*.exe", SearchOption.TopDirectoryOnly);
                var serverExe = rootExes.FirstOrDefault(f => f.ToLower().Contains("server") && !f.ToLower().Contains("unity"));
                if (serverExe != null) return Path.GetFileNameWithoutExtension(serverExe);

                // 2. Look for obvious exe with "server" in ALL subdirs
                var allExes = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories);
                serverExe = allExes.FirstOrDefault(f => f.ToLower().Contains("server") && !f.ToLower().Contains("unity"));
                if (serverExe != null) return Path.GetFileNameWithoutExtension(serverExe);

                // 3. Any exe in ROOT?
                var anyRootExe = rootExes.FirstOrDefault();
                if (anyRootExe != null) return Path.GetFileNameWithoutExtension(anyRootExe);

                // 4. Any exe at all?
                var anyExe = allExes.FirstOrDefault();
                if (anyExe != null) return Path.GetFileNameWithoutExtension(anyExe);
            }
            catch { }
            return null;
        }
        public async Task<string> GetAppNameAsync(string appId)
        {
            try
            {
                var response = await _client.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}");
                var data = JObject.Parse(response);
                if (data[appId] != null && data[appId]["success"]?.Value<bool>() == true)
                {
                    return data[appId]["data"]["name"].ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching app name: {ex.Message}");
            }

            // Fallback: Try SteamCMD
            try 
            {
                var cmdName = await Globals.SteamCMD.GetAppNameFromInfoAsync(appId);
                if (!string.IsNullOrEmpty(cmdName)) return cmdName;
            }
            catch {}

            return null;
        }

    }
}
