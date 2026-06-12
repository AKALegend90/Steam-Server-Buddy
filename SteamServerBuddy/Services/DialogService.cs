using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace SteamServerBuddy.Services
{
    public class DialogService
    {
        public async Task<bool> ConfirmAsync(string title, string message, string confirmText = "Continue")
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow is null)
            {
                return false;
            }

            var result = false;
            var dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 210,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                Background = new SolidColorBrush(Color.Parse("#1A202C"))
            };
            dialog.Content = BuildContent(dialog, message, confirmText, () => result = true);

            await dialog.ShowDialog(desktop.MainWindow);
            return result;

            Control BuildContent(Window owner, string body, string actionText, System.Action onConfirm)
            {
                var cancel = new Button
                {
                    Content = "Cancel",
                    Padding = new Thickness(16, 8),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                cancel.Click += (_, _) => owner.Close();

                var confirm = new Button
                {
                    Content = actionText,
                    Padding = new Thickness(16, 8),
                    Background = new SolidColorBrush(Color.Parse("#E53E3E"))
                };
                confirm.Click += (_, _) =>
                {
                    onConfirm();
                    owner.Close();
                };

                return new Grid
                {
                    Margin = new Thickness(20),
                    RowDefinitions = new RowDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = body,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = Brushes.White,
                            FontSize = 15,
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { cancel, confirm },
                            [Grid.RowProperty] = 1
                        }
                    }
                };
            }
        }
    }
}
