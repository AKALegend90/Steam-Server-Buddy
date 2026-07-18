using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SteamServerBuddy.Services
{
    public class DirectXRuntimeService
    {
        private const string InstallerUrl = "https://download.microsoft.com/download/1/7/1/1718CCC4-6315-4D8E-9543-8E28A4E18C4C/dxwebsetup.exe";
        private static readonly HttpClient Client = new();

        private static readonly string[] LegacyDirectXFiles =
        {
            "d3dx9_43.dll",
            "xinput1_3.dll",
            "xaudio2_7.dll",
            "xapofx1_5.dll"
        };

        public bool IsLegacyRuntimeInstalled()
        {
            var systemDirectories = GetSystemDirectories().ToList();
            if (systemDirectories.Count == 0) return true;

            return LegacyDirectXFiles.All(file =>
                systemDirectories.Any(directory => File.Exists(Path.Combine(directory, file))));
        }

        public async Task<bool> EnsureLegacyRuntimeAsync(Action<string>? statusCallback = null, bool allowPrompt = true)
        {
            if (IsLegacyRuntimeInstalled()) return true;

            if (!allowPrompt)
            {
                statusCallback?.Invoke("DirectX Runtime is missing. Start the server manually once and install the runtime when prompted.");
                return false;
            }

            var install = await Globals.Dialogs.ConfirmAsync(
                "DirectX Runtime Required",
                "This server needs the Microsoft DirectX End-User Runtime before it can start.\n\nSteam Server Buddy can download the official Microsoft installer and run it now. Windows may ask for administrator permission.",
                "Install DirectX");

            if (!install)
            {
                statusCallback?.Invoke("DirectX Runtime installation was cancelled.");
                return false;
            }

            try
            {
                statusCallback?.Invoke("Downloading DirectX Runtime installer...");
                var installerPath = Path.Combine(Path.GetTempPath(), "SteamServerBuddy-dxwebsetup.exe");
                var bytes = await Client.GetByteArrayAsync(InstallerUrl);
                await File.WriteAllBytesAsync(installerPath, bytes);

                statusCallback?.Invoke("Starting DirectX Runtime installer...");
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                if (process == null)
                {
                    statusCallback?.Invoke("DirectX Runtime installer could not be started.");
                    return false;
                }

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    statusCallback?.Invoke($"DirectX Runtime installer exited with code {process.ExitCode}.");
                    return false;
                }

                if (!IsLegacyRuntimeInstalled())
                {
                    statusCallback?.Invoke("DirectX Runtime installer finished, but required files were not detected. Restart Windows and try again.");
                    return false;
                }

                statusCallback?.Invoke("DirectX Runtime installed. Starting server...");
                return true;
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error("DirectX Runtime installation failed", ex);
                statusCallback?.Invoke($"DirectX Runtime installation failed: {ex.Message}");
                return false;
            }
        }

        private static string[] GetSystemDirectories()
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(windows)) return Array.Empty<string>();

            return new[]
            {
                Path.Combine(windows, "System32"),
                Path.Combine(windows, "SysWOW64")
            }
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        }
    }
}
