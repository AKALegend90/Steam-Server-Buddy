using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace SteamServerBuddy.Services
{
    public class SteamCMDService
    {
        private const string SteamCmdUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
        private readonly string _baseDir;
        private readonly string _steamCmdExe;

        private bool _isBootstrapped = false;

        public SteamCMDService()
        {
            _baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamcmd");
            _steamCmdExe = Path.Combine(_baseDir, "steamcmd.exe");
        }

        public string GetSteamCMDPath() => _steamCmdExe;

        public async Task<string> EnsureSteamCMDAsync(Action<string> statusCallback)
        {
            if (File.Exists(_steamCmdExe))
            {
                if (!_isBootstrapped)
                {
                    statusCallback?.Invoke("Bootstrapping SteamCMD (Checking for updates)...");
                    // Login anonymous forces more robust check than just quit
                    await RunSteamCmdAsync(new[] { "+login", "anonymous", "+quit" });

                    // SteamCMD spawns a new process for updates and closes the original. 
                    // We must wait for ALL steamcmd processes to vanish before returning.
                    int loops = 0;
                    while (Process.GetProcessesByName("steamcmd").Length > 0)
                    {
                        if (loops == 0) statusCallback?.Invoke("Waiting for SteamCMD update to finish...");
                        await Task.Delay(1000);
                        loops++;
                        if (loops > 120) // 2 minutes timeout
                        {
                            statusCallback?.Invoke("Warning: Timed out waiting for SteamCMD update.");
                            break; 
                        }
                    }

                    _isBootstrapped = true;
                }
                return _steamCmdExe;
            }

            statusCallback?.Invoke("Downloading SteamCMD...");
            Directory.CreateDirectory(_baseDir);

            using (var client = new HttpClient())
            {
                var bytes = await client.GetByteArrayAsync(SteamCmdUrl);
                var zipPath = Path.Combine(_baseDir, "steamcmd.zip");
                await File.WriteAllBytesAsync(zipPath, bytes);

                statusCallback?.Invoke("Extracting SteamCMD...");
                ZipFile.ExtractToDirectory(zipPath, _baseDir, true);
                File.Delete(zipPath);
            }

            if (File.Exists(_steamCmdExe))
            {
                // Bootstrap
                statusCallback?.Invoke("Bootstrapping SteamCMD (First Run)...");
                await RunSteamCmdAsync(new[] { "+login", "anonymous", "+quit" });
                
                int loops = 0;
                while (Process.GetProcessesByName("steamcmd").Length > 0)
                {
                    if (loops == 0) statusCallback?.Invoke("Waiting for SteamCMD update to finish...");
                    await Task.Delay(1000);
                    loops++;
                     if (loops > 120) break;
                }

                _isBootstrapped = true;
                return _steamCmdExe;
            }

            return null;
        }

        public async Task InstallServerAsync(string appId, string installDir, Action<string> onLog, Action<bool> onDone)
        {
            var exe = await EnsureSteamCMDAsync(onLog);
            if (exe == null)
            {
                onDone?.Invoke(false);
                return;
            }

            // Commands: +force_install_dir <path> +login anonymous +app_update <id> validate +quit
            var args = $"+force_install_dir \"{installDir}\" +login anonymous +app_update {appId} validate +quit";
            Console.WriteLine($"[SteamCMD] Executing: {exe} {args}");
            System.Diagnostics.Debug.WriteLine($"[SteamCMD] Executing: {exe} {args}");
            
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = psi };
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) onLog?.Invoke(e.Data); };
                proc.ErrorDataReceived += (s, e) => { if (e.Data != null) onLog?.Invoke("[ERR] " + e.Data); };

                try
                {
                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                    
                    onLog?.Invoke($"SteamCMD exited with code: {proc.ExitCode}");
                    
                    // SteamCMD exit codes:
                    // 0 = success
                    // 6 = already up to date
                    // 7 = success but needs reboot/restart
                    // 8 = file locked (can sometimes be ignored)
                    bool success = proc.ExitCode == 0 || proc.ExitCode == 6 || proc.ExitCode == 7 || proc.ExitCode == 8;
                    
                    if (!success)
                    {
                        onLog?.Invoke($"Installation may have failed. Check logs above for details.");
                    }
                    
                    onDone?.Invoke(success);
                }
                catch (Exception ex)
                {
                    onLog?.Invoke($"Exception: {ex.Message}");
                    onLog?.Invoke($"Stack trace: {ex.StackTrace}");
                    onDone?.Invoke(false);
                }
            });
        }



        private Task RunSteamCmdAsync(string[] args)
        {
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _steamCmdExe,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p.WaitForExit();
            });
        }
        public async Task<string> GetAppNameFromInfoAsync(string appId)
        {
            var exe = await EnsureSteamCMDAsync(null);
            if (exe == null) return null;

            // Command to get app info
            var args = $"+login anonymous +app_info_update 1 +app_info_print {appId} +quit";
            
            var output = new System.Text.StringBuilder();
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var proc = new Process { StartInfo = psi };
                proc.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                proc.ErrorDataReceived += (s, e) => { }; // Ignore stderr

                try
                {
                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();
                }
                catch { }
            });

            var log = output.ToString();
            // Regex to find "name" "Value"
            // VDF format: "name"		"Palworld Dedicated Server"
            var match = System.Text.RegularExpressions.Regex.Match(log, "\"name\"\\s+\"([^\"]+)\"");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            
            return null;
        }
    }
}
