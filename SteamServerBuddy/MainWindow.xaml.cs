using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Diagnostics;

namespace SteamServerBuddy
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenAuthorGitHub(object? sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/AKALegend90",
                UseShellExecute = true
            });
        }
    }
}
