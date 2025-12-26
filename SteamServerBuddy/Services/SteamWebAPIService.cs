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
        
        // Paths relative to execution directory (or project root, need to be careful)
        // In Python: Path(__file__).parent.parent is project root.
        // In C# Debug: bin/Debug/net8.0/...
        // We should look for "custom_servers.json" in the AppDomain BaseDirectory or specific relative path.
        // For development, we'll assume a fixed relative path or copy to output.
        // Let's assume we want to read from the same place as Python for now? 
        // User workspace: c:\Users\xyooj\Documents\VS code project folder
        
        // Dynamically find project root by looking for "custom_servers.json" in parent folders
        private string ProjectRoot 
        {
            get 
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                while (dir != null && !File.Exists(Path.Combine(dir, "custom_servers.json")))
                {
                    dir = Path.GetDirectoryName(dir);
                    // Specifically check parent of StemServerBuddyCS too
                    if (dir != null && Directory.Exists(Path.Combine(dir, "steam-server-buddy-py"))) return dir;
                }
                return dir ?? @"c:\Users\xyooj\Documents\VS code project folder";
            }
        }
        
        public string CustomServersPath => Path.Combine(ProjectRoot, "custom_servers.json");
        public string UserPathsPath => Path.Combine(ProjectRoot, "steam-server-buddy-py", "manifests", "user_paths.json");

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

            // Priority: 1. Exe name
            var name = FindServerName(installPath);

            // 2. Steam API
            if (string.IsNullOrEmpty(name) || name.Contains("Server"))
            {
                try
                {
                    var response = await _client.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}");
                    var data = JObject.Parse(response);
                    if (data[appId] != null && data[appId]["success"]?.Value<bool>() == true)
                    {
                        name = data[appId]["data"]["name"].ToString();
                    }
                }
                catch { }
            }

            // 3. Folder Name fallback
            if (string.IsNullOrEmpty(name))
            {
                var folderName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                name = !string.IsNullOrEmpty(folderName) ? folderName : $"Server {appId}";
            }

            servers.Add(new ServerInfo
            {
                AppId = appId,
                Name = name,
                InstallPath = installPath,
                IsInstalled = true
            });

            var outputJson = JsonConvert.SerializeObject(servers, Formatting.Indented);
            await File.WriteAllTextAsync(CustomServersPath, outputJson);
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
