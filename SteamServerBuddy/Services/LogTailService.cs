using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SteamServerBuddy.Services
{
    public class LogTailService : IDisposable
    {
        private string _filePath;
        private Action<string> _onLine;
        private CancellationTokenSource _cts;
        private long _lastPos = 0;

        public void Start(string filePath, Action<string> onLine)
        {
            Stop();
            _filePath = filePath;
            _onLine = onLine;
            _cts = new CancellationTokenSource();
            _lastPos = 0;

            Task.Run(() => TailLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private async Task TailLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (File.Exists(_filePath))
                {
                    try
                    {
                        using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (fs.Length < _lastPos) _lastPos = 0; // Rotated?
                            fs.Seek(_lastPos, SeekOrigin.Begin);

                            using (var sr = new StreamReader(fs, Encoding.UTF8))
                            {
                                while (!sr.EndOfStream)
                                {
                                    var line = await sr.ReadLineAsync();
                                    if (line != null) _onLine?.Invoke(line);
                                }
                                _lastPos = fs.Position;
                            }
                        }
                    }
                    catch { /* File locked or error */ }
                }
                await Task.Delay(1000, token); // Optimized: Check every 1 second instead of 500ms
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
