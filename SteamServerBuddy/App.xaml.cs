using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SteamServerBuddy.ViewModels;
using SteamServerBuddy.Views;

namespace SteamServerBuddy
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            Globals.Theme.Apply(Globals.AppSettings.GetTheme());

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                Globals.Automation.Start();
                desktop.ShutdownRequested += (_, _) => Globals.Automation.Dispose();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
