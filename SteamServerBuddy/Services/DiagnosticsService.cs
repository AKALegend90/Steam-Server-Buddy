using System;
using System.IO;
using System.Threading;

namespace SteamServerBuddy.Services
{
    public class DiagnosticsService
    {
        private readonly string _logPath;
        private readonly object _lock = new();

        public DiagnosticsService()
        {
            AppPaths.EnsureDataDirectories();
            _logPath = Path.Combine(AppPaths.DataDir, "steam-server-buddy.log");
        }

        public string LogPath => _logPath;

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message, Exception? ex = null)
        {
            var text = ex == null ? message : $"{message}: {ex.Message}{Environment.NewLine}{ex}";
            Write("ERROR", text);
        }

        private void Write(string level, string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
                lock (_lock)
                {
                    File.AppendAllText(_logPath, line);
                }
            }
            catch
            {
                // Diagnostics must never break the app.
            }
        }
    }
}
