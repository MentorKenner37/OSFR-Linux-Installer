using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OSFR.Linux.Installer.Services;

namespace OSFR.Linux.Installer;

public partial class MainWindow : Window
{
    private readonly InstallService _installService = new();
    private SystemState _state = SystemDetector.Detect();
    private bool _busy;

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#45D483"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#E05252"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#91A0B8"));

    public MainWindow()
    {
        InitializeComponent();
        InstallPathBox.Text = InstallService.DefaultInstallRoot;
        RefreshState();
    }

    private string InstallRoot => string.IsNullOrWhiteSpace(InstallPathBox.Text)
        ? InstallService.DefaultInstallRoot
        : InstallService.NormalizeInstallRoot(InstallPathBox.Text);

    private void RefreshState()
    {
        _state = SystemDetector.Detect();
        SetCheck(LinuxStatus, _state.IsLinux, _state.IsLinux ? "READY" : "REQUIRED");
        SetCheck(CpuStatus, _state.IsX64, _state.IsX64 ? "READY" : "REQUIRED");
        SetCheck(SteamStatus, _state.SteamRoot is not null, _state.SteamRoot is not null ? "DETECTED" : "NOT FOUND");
        SetCheck(ProtonStatus, _state.ProtonPath is not null, _state.ProtonPath is not null ? "DETECTED" : "NOT FOUND");

        var installed = _installService.IsInstalled(InstallRoot);
        ActionButton.Content = installed ? "UNINSTALL" : "INSTALL";
        ActionButton.IsEnabled = !_busy && (installed || _state.Ready);

        HeroStatus.Text = installed
            ? "OSFR IS INSTALLED"
            : _state.Ready ? "READY TO INSTALL" : "SYSTEM REQUIREMENTS NOT MET";
        HeroStatus.Foreground = installed || _state.Ready ? Good : Bad;

        DetailText.Text = _state.Ready
            ? $"Steam: {_state.SteamRoot}\nProton: {_state.ProtonPath}"
            : "This installer requires x86_64 Linux, Steam, and an installed Proton build.";

        if (!_busy)
        {
            StatusText.Text = installed ? "OSFR is installed. Uninstall is available." : "Choose an installation location, then install OSFR.";
            ProgressText.Text = "Ready";
        }
    }

    private static void SetCheck(TextBlock control, bool ok, string text)
    {
        control.Text = ok ? $"✓ {text}" : $"✗ {text}";
        control.Foreground = ok ? Good : Bad;
    }

    private async void BrowseClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose OSFR installation location",
            AllowMultiple = false
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            InstallPathBox.Text = path;
            RefreshState();
        }
    }

    private async void ActionClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        if (_installService.IsInstalled(InstallRoot))
            await UninstallAsync();
        else
            await InstallAsync();
    }

    private async Task InstallAsync()
    {
        RefreshState();
        if (!_state.Ready)
        {
            await ShowMessageAsync("Requirements not met", "Linux x86_64, Steam, and Proton must be installed first.");
            return;
        }

        SetBusy(true);
        var progress = new Progress<InstallProgress>(UpdateProgress);

        try
        {
            await _installService.InstallAsync(InstallRoot, _state, progress);
            _installService.Launch(InstallRoot);
            await ShowMessageAsync("Installation complete", "Open Source Free Realms has been installed successfully.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Installation failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshState();
        }
    }

    private async Task UninstallAsync()
    {
        var confirmed = await ConfirmAsync(
            "Uninstall OSFR",
            "Remove the OSFR Launcher, Proton prefix, all downloaded server clients, OSFR data, and shortcuts? The installer itself will be preserved.");
        if (!confirmed)
            return;

        SetBusy(true);
        var progress = new Progress<InstallProgress>(UpdateProgress);

        try
        {
            await _installService.UninstallAsync(InstallRoot, progress);
            await ShowMessageAsync("Uninstall complete", "OSFR and all downloaded server/client data have been removed.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Uninstall failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
            RefreshState();
        }
    }

    private void UpdateProgress(InstallProgress value)
    {
        Progress.Value = value.Percent;
        StatusText.Text = value.Message;
        ProgressText.Text = $"{value.Percent}%";
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        ActionButton.IsEnabled = !busy;
        InstallPathBox.IsEnabled = !busy;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var yes = new Button { Content = "YES", MinWidth = 90 };
        var no = new Button { Content = "NO", MinWidth = 90 };
        var dialog = BuildDialog(title, message, yes, no);
        var result = false;
        yes.Click += (_, _) => { result = true; dialog.Close(); };
        no.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var ok = new Button { Content = "OK", MinWidth = 90 };
        var dialog = BuildDialog(title, message, ok);
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    private static Window BuildDialog(string title, string message, params Button[] buttons)
    {
        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        foreach (var button in buttons)
            buttonPanel.Children.Add(button);

        return new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(24),
                Spacing = 20,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Muted },
                    buttonPanel
                }
            }
        };
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
}
