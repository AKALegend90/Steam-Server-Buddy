using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SteamServerBuddy.Views;

public sealed class PalworldPortCheckWindow : Window
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly TextBox _publicIp = new() { IsReadOnly = true };
    private readonly TextBlock _serviceStatus = new() { Text = "Not checked yet." };
    private readonly Dictionary<(string Protocol, int Port), TextBlock> _statuses = new();
    private readonly Button _checkButton = new() { Content = "Check", Padding = new Thickness(24, 9) };

    private static readonly (string Label, string Protocol, int Port)[] Ports =
    {
        ("Game port (UDP)", "udp", 8211),
        ("Steam/Query port (UDP)", "udp", 27015),
        ("REST API port (TCP)", "tcp", 8212),
        ("RCON port (TCP)", "tcp", 25575)
    };

    public PalworldPortCheckWindow()
    {
        Title = "Palworld Port Check";
        Width = 640;
        Height = 500;
        MinWidth = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#182333"));
        Content = BuildContent();
        _checkButton.Click += async (_, _) => await RunChecksAsync();
        Opened += async (_, _) => await LoadPublicIpAsync();
    }

    private Control BuildContent()
    {
        var rows = new StackPanel { Spacing = 10 };
        rows.Children.Add(ResultRow("Port check service online", null, _serviceStatus));
        foreach (var port in Ports)
        {
            var status = new TextBlock { Text = "Not checked yet.", VerticalAlignment = VerticalAlignment.Center };
            _statuses[(port.Protocol, port.Port)] = status;
            rows.Children.Add(ResultRow(port.Label, port.Port, status));
        }

        var close = new Button { Content = "Close", Padding = new Thickness(24, 9) };
        close.Click += (_, _) => Close();

        return new Grid
        {
            Margin = new Thickness(18),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = "Checks whether Palworld's default ports can be reached from the internet using Check-Host. Run this with the server stopped so the temporary test listeners can bind.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#B8C4D6"))
                },
                LabeledRow("Your public IP", _publicIp, 1),
                new TextBlock { Text = "Results", FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0,14,0,8), [Grid.RowProperty] = 2 },
                new ScrollViewer { Content = rows, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, [Grid.RowProperty] = 3 },
                new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#493A20")), Padding = new Thickness(12), Margin = new Thickness(0,14,0,12),
                    Child = new TextBlock
                    {
                        Text = "UDP checks can be inconclusive when a game protocol does not answer a generic probe. REST and RCON are administrative ports and normally should NOT be exposed to the internet unless you intentionally secured and forwarded them.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.Parse("#FFD56A"))
                    },
                    [Grid.RowProperty] = 4
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10,
                    Children = { _checkButton, close }, [Grid.RowProperty] = 5
                }
            }
        };
    }

    private static Control LabeledRow(string label, Control value, int row) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("190,*"), Margin = new Thickness(0,14,0,0),
        Children =
        {
            new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
            value.WithColumn(1)
        },
        [Grid.RowProperty] = row
    };

    private static Control ResultRow(string label, int? port, TextBlock status)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("190,80,*") };
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        if (port.HasValue)
            grid.Children.Add(new TextBox { Text = port.Value.ToString(), IsReadOnly = true, [Grid.ColumnProperty] = 1 });
        status[Grid.ColumnProperty] = 2;
        status.Margin = new Thickness(12,0,0,0);
        grid.Children.Add(status);
        return grid;
    }

    private async Task LoadPublicIpAsync()
    {
        try
        {
            _publicIp.Text = (await Client.GetStringAsync("https://api.ipify.org")).Trim();
            SetStatus(_serviceStatus, "Online", true);
        }
        catch
        {
            _publicIp.Text = "Unable to detect";
            SetStatus(_serviceStatus, "Offline or unavailable", false);
        }
    }

    private async Task RunChecksAsync()
    {
        if (string.IsNullOrWhiteSpace(_publicIp.Text) || _publicIp.Text == "Unable to detect")
            await LoadPublicIpAsync();
        if (string.IsNullOrWhiteSpace(_publicIp.Text) || _publicIp.Text == "Unable to detect") return;

        _checkButton.IsEnabled = false;
        foreach (var status in _statuses.Values) SetStatus(status, "Checking...", null);
        foreach (var port in Ports)
        {
            var reachable = await CheckHostAsync(_publicIp.Text, port.Protocol, port.Port);
            SetStatus(_statuses[(port.Protocol, port.Port)],
                reachable == true ? "Reachable" : reachable == false ? "Not reachable" : "Inconclusive",
                reachable);
        }
        _checkButton.IsEnabled = true;
    }

    private static async Task<bool?> CheckHostAsync(string host, string protocol, int port)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://check-host.net/check-{protocol}?host={Uri.EscapeDataString(host + ":" + port)}&max_nodes=3");
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using var started = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (!started.RootElement.TryGetProperty("request_id", out var requestId)) return null;
            await Task.Delay(3500);
            using var resultRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://check-host.net/check-result/{requestId.GetString()}");
            resultRequest.Headers.Accept.ParseAdd("application/json");
            using var resultResponse = await Client.SendAsync(resultRequest);
            using var results = JsonDocument.Parse(await resultResponse.Content.ReadAsStringAsync());
            var sawResult = false;
            foreach (var node in results.RootElement.EnumerateObject())
            {
                if (node.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
                sawResult = true;
                var text = node.Value.ToString().ToLowerInvariant();
                if (!text.Contains("error") && !text.Contains("refused") && !text.Contains("timed out")) return true;
            }
            return sawResult ? false : null;
        }
        catch { return null; }
    }

    private static void SetStatus(TextBlock block, string text, bool? success)
    {
        block.Text = text;
        block.Foreground = new SolidColorBrush(success == true ? Color.Parse("#54D98C") :
            success == false ? Color.Parse("#FF6B6B") : Color.Parse("#A9B4C5"));
    }
}

internal static class PortCheckGridExtensions
{
    public static T WithColumn<T>(this T control, int column) where T : Control
    {
        control[Grid.ColumnProperty] = column;
        return control;
    }
}
