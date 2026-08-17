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
    private bool _updatingProtonSelection;

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#45D483"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#E05252"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#91A0B8"));

    public MainWindow()
    {
        InitializeComponent();
        InstallPathBox.Text = InstallService.DefaultInstallRoot;
        RefreshState();
    }

    private string InstallRoot => InstallService.NormalizeInstallRoot(InstallPathBox.Text ?? InstallService.DefaultInstallRoot);

    private void RefreshState()
    {
        var previousProton = (ProtonComboBox.SelectedItem as ProtonCandidate)?.Path;
        _state = SystemDetector.Detect();

        _updatingProtonSelection = true;
        try
        {
            ProtonComboBox.ItemsSource = _state.ProtonCandidates;
            var selected = _state.ProtonCandidates?.FirstOrDefault(p => p.Path == previousProton)
                           ?? _state.ProtonCandidates?.FirstOrDefault();
            ProtonComboBox.SelectedItem = selected;
            ProtonComboBox.IsEnabled = !_busy && (_state.ProtonCandidates?.Count ?? 0) > 0;
            if (selected is not null)
                _state = _state.WithProton(selected.Path);
        }
        finally
        {
            _updatingProtonSelection = false;
        }

        SetCheck(LinuxStatus, _state.IsLinux, _state.IsLinux ? "READY" : "REQUIRED");
        SetCheck(CpuStatus, _state.IsX64, _state.IsX64 ? "READY" : "REQUIRED");
        SetCheck(SteamStatus, _state.SteamRoot is not null, _state.SteamRoot is not null ? "DETECTED" : "NOT FOUND");
        SetCheck(ProtonStatus, _state.ProtonPath is not null, _state.ProtonPath is not null ? "DETECTED" : "NOT FOUND");

        DetailText.Text = _state.Ready
            ? $"Steam: {_state.SteamRoot}\nProton: {_state.ProtonPath}"
            : "This installer requires x86_64 Linux, Steam, and an installed Proton build.";

        RefreshInstallUi();
    }

    private void RefreshInstallUi()
    {
        var pathError = RefreshInstallPathState();
        var installed = false;

        if (pathError is null)
        {
            try
            {
                installed = _installService.IsInstalled(InstallRoot);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                pathError = "The installation path is not valid.";
                InstallPathStatus.Text = $"✗ {pathError}";
                InstallPathStatus.Foreground = Bad;
            }
        }

        ActionButton.Content = installed ? "UNINSTALL" : "INSTALL";
        ActionButton.IsEnabled = !_busy && (installed || (_state.Ready && pathError is null));

        HeroStatus.Text = installed
            ? "OSFR IS INSTALLED"
            : !_state.Ready
                ? "SYSTEM REQUIREMENTS NOT MET"
                : pathError is not null
                    ? "INSTALL LOCATION NEEDS ATTENTION"
                    : "READY TO INSTALL";
        HeroStatus.Foreground = installed || (_state.Ready && pathError is null) ? Good : Bad;

        if (!_busy)
        {
            StatusText.Text = installed
                ? "OSFR is installed. Uninstall is available."
                : pathError is not null
                    ? "Choose a valid installation location."
                    : "Choose an installation location, then install OSFR.";
            ProgressText.Text = "Ready";
        }
    }

    private string? RefreshInstallPathState()
    {
        var path = InstallPathBox.Text ?? string.Empty;
        var error = InstallService.GetInstallDestinationError(path);

        if (error is not null)
        {
            InstallPathStatus.Text = $"✗ {error}";
            InstallPathStatus.Foreground = Bad;
            return error;
        }

        var installed = false;
        try
        {
            installed = _installService.IsInstalled(InstallService.NormalizeInstallRoot(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            InstallerLog.Warn($"Could not validate install path: {ex.Message}");
        }

        InstallPathStatus.Text = installed
            ? "✓ Existing OSFR installation detected."
            : "✓ Installation location is ready.";
        InstallPathStatus.Foreground = Good;
        return null;
    }

    private static void SetCheck(TextBlock control, bool ok, string text)
    {
        control.Text = ok ? $"✓ {text}" : $"✗ {text}";
        control.Foreground = ok ? Good : Bad;
    }

    private void ProtonSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingProtonSelection || ProtonComboBox.SelectedItem is not ProtonCandidate selected)
            return;

        _state = _state.WithProton(selected.Path);
        DetailText.Text = $"Steam: {_state.SteamRoot}\nProton: {selected.Path}";
        InstallerLog.Info($"User selected Proton: {selected.Path}");
        RefreshInstallUi();
    }

    private void InstallPathChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_busy)
            RefreshInstallUi();
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
            InstallPathBox.Text = path;
    }

    private async void ActionClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy)
            return;

        try
        {
            if (_installService.IsInstalled(InstallRoot))
                await UninstallAsync();
            else
                await InstallAsync();
        }
        catch (Exception ex)
        {
            InstallerLog.Error("Action failed before operation started", ex);
            await ShowMessageAsync("Operation failed", ex.Message);
        }
    }

    private async Task InstallAsync()
    {
        // Re-detect Steam/Proton but preserve a user-selected Proton path when it still exists.
        RefreshState();
        if (!_state.Ready)
        {
            await ShowMessageAsync("Requirements not met", "Linux x86_64, Steam, and Proton must be installed first.");
            return;
        }

        var pathError = InstallService.GetInstallDestinationError(InstallPathBox.Text ?? string.Empty);
        if (pathError is not null)
        {
            await ShowMessageAsync("Invalid installation location", pathError);
            return;
        }

        SetBusy(true);
        var progress = new Progress<InstallProgress>(UpdateProgress);
        var shouldClose = false;

        try
        {
            await _installService.InstallAsync(InstallRoot, _state, progress);
            _installService.Launch(InstallRoot);
            shouldClose = CloseAfterInstallCheck.IsChecked == true;
            if (!shouldClose)
                await ShowMessageAsync("Installation complete", "Open Source Free Realms has been installed successfully and the launcher has started.");
        }
        catch (Exception ex)
        {
            InstallerLog.Error("Installation UI flow failed", ex);
            await ShowMessageAsync("Installation failed", $"{ex.Message}\n\nDiagnostics: {InstallerLog.LogPath}");
        }
        finally
        {
            SetBusy(false);
            if (!shouldClose)
                RefreshState();
        }

        if (shouldClose)
            Close();
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
            InstallerLog.Error("Uninstall failed", ex);
            await ShowMessageAsync("Uninstall failed", $"{ex.Message}\n\nDiagnostics: {InstallerLog.LogPath}");
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
        CloseAfterInstallCheck.IsEnabled = !busy;
        ProtonComboBox.IsEnabled = !busy && (_state.ProtonCandidates?.Count ?? 0) > 0;
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
