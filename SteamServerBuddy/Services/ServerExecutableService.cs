using System;
using System.IO;
using System.Linq;

namespace SteamServerBuddy.Services
{
    public class ServerExecutableService
    {
        public string? FindServerExecutable(string installPath)
        {
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath)) return null;

            try
            {
                var bat = Directory.GetFiles(installPath, "*.bat", SearchOption.AllDirectories)
                    .Where(IsGoodLaunchScript)
                    .OrderBy(f => Path.GetFileName(f).Length)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(bat)) return bat;

                var exes = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories)
                    .Where(IsLikelyServerExecutable)
                    .ToList();

                var preferred = exes.FirstOrDefault(f => ContainsAny(f, "palworld", "enshrouded_server", "valheim_server", "vrisingserver"));
                if (!string.IsNullOrWhiteSpace(preferred)) return preferred;

                preferred = exes.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Contains("server", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(preferred)) return preferred;

                return exes.OrderBy(f => f.Split(Path.DirectorySeparatorChar).Length).ThenBy(f => f.Length).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Globals.Diagnostics.Error($"Executable detection failed for {installPath}", ex);
                return null;
            }
        }

        private static bool IsGoodLaunchScript(string path)
        {
            var name = Path.GetFileName(path);
            return name.Contains("start", StringComparison.OrdinalIgnoreCase)
                || name.Contains("run", StringComparison.OrdinalIgnoreCase)
                || name.Contains("server", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyServerExecutable(string path)
        {
            var name = Path.GetFileName(path);
            return !ContainsAny(name, "unity", "crash", "steam", "redist", "vc_redist", "dotnet", "unins");
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            return needles.Any(n => value.Contains(n, StringComparison.OrdinalIgnoreCase));
        }
    }
}
