using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace SteamServerBuddy.Services
{
    public class AppThemeService
    {
        public const string DarkTheme = "Dark";
        public const string LightTheme = "Light";

        public void Apply(string? theme)
        {
            var normalizedTheme = Normalize(theme);
            var app = Application.Current;
            if (app is null) return;

            app.RequestedThemeVariant = normalizedTheme == LightTheme
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

            var palette = normalizedTheme == LightTheme
                ? CreateLightPalette()
                : CreateDarkPalette();

            foreach (var (key, value) in palette)
            {
                app.Resources[key] = new SolidColorBrush(Color.Parse(value));
            }
        }

        public string Normalize(string? theme)
        {
            return string.Equals(theme, LightTheme, System.StringComparison.OrdinalIgnoreCase)
                ? LightTheme
                : DarkTheme;
        }

        private static Dictionary<string, string> CreateDarkPalette() => new()
        {
            ["AppBackgroundBrush"] = "#07101B",
            ["AppSidebarBrush"] = "#08111D",
            ["AppPanelBrush"] = "#111A26",
            ["AppPanelAltBrush"] = "#101A28",
            ["AppCardBrush"] = "#252B36",
            ["AppCardAltBrush"] = "#172131",
            ["AppBorderBrush"] = "#3A4556",
            ["AppTextBrush"] = "#FFFFFF",
            ["AppSubtleTextBrush"] = "#A0AEC0",
            ["AppMutedTextBrush"] = "#718096",
            ["AppInfoTextBrush"] = "#90CDF4",
            ["AppConsoleBackgroundBrush"] = "#08101A",
            ["AppConsoleTextBrush"] = "#BEE3F8"
        };

        private static Dictionary<string, string> CreateLightPalette() => new()
        {
            ["AppBackgroundBrush"] = "#F5F7FB",
            ["AppSidebarBrush"] = "#FFFFFF",
            ["AppPanelBrush"] = "#FFFFFF",
            ["AppPanelAltBrush"] = "#F8FAFC",
            ["AppCardBrush"] = "#FFFFFF",
            ["AppCardAltBrush"] = "#F1F5F9",
            ["AppBorderBrush"] = "#CBD5E1",
            ["AppTextBrush"] = "#0F172A",
            ["AppSubtleTextBrush"] = "#475569",
            ["AppMutedTextBrush"] = "#64748B",
            ["AppInfoTextBrush"] = "#2563EB",
            ["AppConsoleBackgroundBrush"] = "#0F172A",
            ["AppConsoleTextBrush"] = "#DBEAFE"
        };
    }
}
