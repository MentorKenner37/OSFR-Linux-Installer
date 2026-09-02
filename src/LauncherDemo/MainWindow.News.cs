using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace OSFR.Linux.LauncherDemo;

public partial class MainWindow
{
    private Control? _serverPlayPanel;
    private Window? _serverPlayWindow;

    private void InitializeNewsHome()
    {
        if (HomePage.Content is not Control oldHome)
            return;

        _serverPlayPanel = oldHome;
        HomePage.Content = BuildNewsPage();
    }

    private Control BuildNewsPage()
    {
        var root = new StackPanel { Margin = new Thickness(34, 30), Spacing = 18 };
        root.Children.Add(new TextBlock
        {
            Text = "NEWS",
            FontSize = 25,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        });
        root.Children.Add(new TextBlock
        {
            Text = "Sanctuary Linux Launcher news and announcements.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap
        });

        var welcome = new StackPanel { Spacing = 8 };
        welcome.Children.Add(new TextBlock
        {
            Text = "WELCOME TO SANCTUARY LINUX LAUNCHER",
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Good
        });
        welcome.Children.Add(new TextBlock
        {
            Text = "Launcher announcements, release notes, Linux compatibility notices, and other project updates will live here. Server login and launch controls now open from the Servers page.",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        });
        welcome.Children.Add(new TextBlock
        {
            Text = "News delivery is not wired to a remote feed yet. This page is intentionally ready for that next step.",
            Foreground = Muted,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        });

        root.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#151515")),
            BorderBrush = new SolidColorBrush(Color.Parse("#303030")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(22),
            Child = welcome
        });

        return root;
    }

    private async Task OpenServerPlayPopupAsync(SavedServerListItem selected)
    {
        if (_serverPlayPanel is null)
            return;

        if (_serverPlayWindow is not null)
        {
            _serverPlayWindow.Activate();
            return;
        }

        ServerUrlBox.Text = selected.Url;
        LoadRememberedForCurrentServer();

        var host = new Grid();
        host.Children.Add(_serverPlayPanel);

        var popup = new Window
        {
            Title = $"{selected.DisplayName} — Sanctuary Linux Launcher",
            Width = 820,
            Height = 720,
            MinWidth = 700,
            MinHeight = 620,
            Content = new ScrollViewer { Content = host }
        };
        _serverPlayWindow = popup;
        popup.Closed += (_, _) =>
        {
            if (popup.Content is ScrollViewer scroll)
                scroll.Content = null;
            host.Children.Clear();
            _serverPlayWindow = null;
        };

        await popup.ShowDialog(this);
    }
}
