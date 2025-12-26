using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using SteamServerBuddy.Models;
using SteamServerBuddy.Services;
using SteamServerBuddy.ViewModels;

namespace SteamServerBuddy
{
    public partial class App : Application
    {
        private static bool _isDisplayingError = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global Exception Handler
            this.DispatcherUnhandledException += (s, args) =>
            {
                if (_isDisplayingError) return;
                _isDisplayingError = true;
                try 
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                    File.AppendAllText(logPath, $"{DateTime.Now}: {args.Exception}\n\n");
                    MessageBox.Show($"Application error: {args.Exception.Message}", "Error");
                }
                catch { }
                finally { _isDisplayingError = false; }
                args.Handled = true; // Prevent crash if possible
            };

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
