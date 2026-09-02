using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace OSFR.Linux.LauncherDemo;

public partial class MainWindow
{
    private Control? _serverPlayPanel;
    private bool _serverPlayPanelAttached;
    private bool _newsInitialized;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        InitializeNewsHome();
    }

    private void InitializeNewsHome()
    {
        if (_newsInitialized || HomePage.Content is not StackPanel oldHome)
            return;

        _newsInitialized = true;

        // The old Home page contains:
        // 0 header, 1 server-address tile, 2 server metadata tile,
        // 3 account tile, 4 launch button, 5 launch status.
        // Keep only the account/launch controls for the inline Servers flow.
        while (oldHome.Children.Count > 6)
            oldHome.Children.RemoveAt(oldHome.Children.Count - 1);

        if (oldHome.Children.Count >= 3)
        {
            oldHome.Children.RemoveAt(2);
            oldHome.Children.RemoveAt(1);
            oldHome.Children.RemoveAt(0);
        }

        oldHome.Margin = new Thickness(0, 8, 0, 0);
        _serverPlayPanel = oldHome;
        HomePage.Content = BuildNewsPage();

        foreach (var text in this.GetVisualDescendants().OfType<TextBlock>())
        {
            if (string.Equals(text.Text, "HOME", StringComparison.Ordinal))
                text.Text = "NEWS";
        }
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
            Text = "Launcher announcements, release notes, Linux compatibility notices, and other project updates will live here. Server accounts and launch controls stay inside the Servers page.",
            Foreground = Brushes.LightGray,
            TextWrapping = TextWrapping.Wrap
        });
        welcome.Children.Add(new TextBlock
        {
            Text = "News delivery is not wired to a remote feed yet. This page is ready for that next step.",
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

        ServerUrlBox.Text = selected.Url;
        LoadRememberedForCurrentServer();

        if (!_serverPlayPanelAttached && ServersPage.Content is StackPanel serversRoot)
        {
            var accountSection = new StackPanel { Spacing = 8 };
            accountSection.Children.Add(new TextBlock
            {
                Text = "SERVER ACCOUNT",
                FontSize = 12,
                FontWeight = FontWeight.Bold,
                Foreground = Good
            });
            accountSection.Children.Add(new TextBlock
            {
                Text = "Sign in to the selected server, then launch Sanctuary.",
                Foreground = Muted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
            accountSection.Children.Add(_serverPlayPanel);

            serversRoot.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#151515")),
                BorderBrush = new SolidColorBrush(Color.Parse("#303030")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Child = accountSection
            });
            _serverPlayPanelAttached = true;
        }

        ShowPage(ServersPage);
        await JoinCurrentServerAsync();
    }
}
