using System;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamServerBuddy.Services;

namespace SteamServerBuddy.ViewModels
{
    public partial class ConsoleViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private string _serverName;

        [ObservableProperty]
        private string _logContent = "";

        [ObservableProperty]
        private bool _autoScroll = true;

        [ObservableProperty]
        private bool _isEmbedded;

        private LogTailService _tailService = new LogTailService();
        private StringBuilder _buffer = new StringBuilder();

        public ConsoleViewModel()
        {
            ClearCommand = new RelayCommand(Clear);
        }

        public IRelayCommand ClearCommand { get; }

        public void Load(string serverName, string logPath)
        {
            ServerName = serverName;
            LogContent = "Connecting to log stream...\n";
            _buffer.Clear();
            _tailService.Start(logPath, line => 
            {
                _buffer.AppendLine(line);
                // Simple capping to prevent memory leak
                if (_buffer.Length > 100000) _buffer.Remove(0, 20000);
                
                // Update on UI thread
                App.Current.Dispatcher.Invoke(() => 
                {
                    LogContent = _buffer.ToString();
                });
            });
        }

        private void Clear()
        {
            _buffer.Clear();
            LogContent = "";
        }

        public void Dispose()
        {
            _tailService?.Dispose();
        }
    }
}
