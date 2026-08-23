using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using OSFR.Linux.Installer.Services;

namespace OSFR.Linux.Installer;

public partial class MainWindow : Window
{
    private readonly InstallService _installService = new();
    private SystemState _state = SystemDetector.Detect();
    private HostSnapshot _host = DetectHost();
    private bool _busy;
    private bool _updatingProtonSelection;
    private bool _maintenanceMode;
    private bool _updatingMaintenanceShortcut;
    private bool _updatingMaintenanceDisplayMode;
    private InstallationInfo _installationInfo = new(InstallationCondition.NotInstalled, null, false);
    private int _step = 1;

    private sealed record HostSnapshot(
        string OperatingSystem,
        string Kernel,
        string Architecture,
        string Cpu,
        string Memory,
        string Gpu,
        string Desktop,
        string Session);

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#45D483"));
    private static readonly IBrush Bad = new SolidColorBrush(Color.Parse("#E05252"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#91A0B8"));
    private static readonly IBrush Active = new SolidColorBrush(Color.Parse("#2F9E5A"));
    private static readonly IBrush ActiveText = new SolidColorBrush(Color.Parse("#4CCF7A"));
    private static readonly IBrush Inactive = new SolidColorBrush(Color.Parse("#232323"));
    private static readonly IBrush InactiveBorder = new SolidColorBrush(Color.Parse("#414141"));
    private static readonly IBrush InactiveText = new SolidColorBrush(Color.Parse("#9A9A9A"));

    public MainWindow()
    {
        InitializeComponent();
        ApplyBranding();
        InstallPathBox.Text = InstallerState.GetInitialInstallRoot();
        RefreshState();
        UpdateStepUi();
    }

    private string SelectedGraphicsBackend =>
        GraphicsBackendComboBox.SelectedIndex == 1 ? GraphicsBackendConfig.WineD3D : GraphicsBackendConfig.Dxvk;

    private string SelectedDisplayMode =>
        FullscreenCheck.IsChecked == true ? DisplayModeConfig.Fullscreen : DisplayModeConfig.Windowed;

    private (int Width, int Height) DesktopResolution
    {
        get
        {
            var screen = Screens.Primary;
            return screen is null
                ? (1920, 1080)
                : (Math.Max(640, screen.Bounds.Width), Math.Max(480, screen.Bounds.Height));
        }
    }

    private void ApplyBranding()
    {
        try
        {
            using var windowIconStream = OsfrBranding.OpenIconStream();
            Icon = new WindowIcon(windowIconStream);

            using var brandIconStream = OsfrBranding.OpenIconStream();
            BrandIcon.Source = new Bitmap(brandIconStream);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or FormatException)
        {
            InstallerLog.Warn($"Could not load Sanctuary branding icon: {ex.Message}");
        }
    }

    private void ReapplyWindowIcon()
    {
        try
        {
            using var stream = OsfrBranding.OpenIconStream();
            Icon = new WindowIcon(stream);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or FormatException)
        {
            InstallerLog.Warn($"Could not reapply Sanctuary window icon: {ex.Message}");
        }
    }

    private string InstallRoot => InstallService.NormalizeInstallRoot(InstallPathBox.Text ?? InstallService.DefaultInstallRoot);

    private void RefreshState()
    {
        var previousProton = (ProtonComboBox.SelectedItem as ProtonCandidate)?.Path;
        _state = SystemDetector.Detect();
        _host = DetectHost();
        var compatibleCandidates = _state.ProtonCandidates?.Where(candidate => candidate.Compatible).ToList() ?? [];

        _updatingProtonSelection = true;
        ProtonCandidate? selected = null;
        try
        {
            ProtonComboBox.ItemsSource = compatibleCandidates;
            selected = compatibleCandidates.FirstOrDefault(candidate => candidate.Path == previousProton)
                       ?? compatibleCandidates.FirstOrDefault(candidate => candidate.Recommended)
                       ?? compatibleCandidates.FirstOrDefault();
            ProtonComboBox.SelectedItem = selected;
            ProtonComboBox.IsEnabled = !_busy && compatibleCandidates.Count > 0;
            if (selected is not null)
                _state = _state.WithProton(selected);
        }
        finally
        {
            _updatingProtonSelection = false;
        }

        SetCheck(LinuxStatus, _state.IsLinux, _state.IsLinux ? _host.OperatingSystem : "Linux required");
        SetCheck(CpuStatus, _state.IsX64, _state.IsX64 ? $"{_host.Cpu} ({_host.Architecture})" : $"{_host.Architecture} — x86_64 required");
        SetCheck(SteamStatus, _state.SteamRoot is not null, _state.SteamRoot is not null ? "DETECTED" : "NOT FOUND");
        SetCheck(ProtonStatus, _state.ProtonCompatible, selected?.Name ?? (_state.ProtonCompatible ? "COMPATIBLE" : "NOT FOUND / INCOMPATIBLE"));

        DetailText.Text = BuildHostDetails();

        RefreshInstallUi();
    }

    private string BuildHostDetails()
    {
        var lines = new List<string>
        {
            $"OS: {_host.OperatingSystem}",
            $"Kernel: {_host.Kernel}",
            $"Desktop: {_host.Desktop} ({_host.Session})",
            $"CPU: {_host.Cpu}",
            $"Memory: {_host.Memory}",
            $"GPU: {_host.Gpu}",
            $"Steam: {_state.SteamRoot ?? "not found"}",
            $"Proton: {_state.ProtonPath ?? "not found"}"
        };

        lines.Add($"curl fallback: {_state.CurlPath ?? "not found (normal downloads remain available)"}");

        if (!string.IsNullOrWhiteSpace(_state.ProtonCompatibilityMessage))
            lines.Add(_state.ProtonCompatibilityMessage);

        if (!_state.Ready)
            lines.Add("A supported x86_64 Linux environment, Steam, and a compatible installed Proton build are required before installation can continue.");

        return string.Join(Environment.NewLine, lines);
    }

    private static HostSnapshot DetectHost()
    {
        return new HostSnapshot(
            ReadOperatingSystemName(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
            ReadCpuModel(),
            ReadMemoryTotal(),
            ReadGpuModel(),
            ReadDesktop(),
            Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "unknown");
    }

    private static string ReadOperatingSystemName()
    {
        const string osRelease = "/etc/os-release";
        try
        {
            if (!File.Exists(osRelease))
                return RuntimeInformation.OSDescription;

            foreach (var line in File.ReadLines(osRelease))
            {
                if (!line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                    continue;

                return line["PRETTY_NAME=".Length..].Trim().Trim('"');
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read /etc/os-release: {ex.Message}");
        }

        return RuntimeInformation.OSDescription;
    }

    private static string ReadCpuModel()
    {
        const string cpuInfo = "/proc/cpuinfo";
        try
        {
            if (File.Exists(cpuInfo))
            {
                foreach (var line in File.ReadLines(cpuInfo))
                {
                    if (!line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var separator = line.IndexOf(':');
                    if (separator >= 0 && separator + 1 < line.Length)
                        return line[(separator + 1)..].Trim();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read CPU information: {ex.Message}");
        }

        return RuntimeInformation.ProcessArchitecture.ToString();
    }

    private static string ReadMemoryTotal()
    {
        const string memInfo = "/proc/meminfo";
        try
        {
            if (File.Exists(memInfo))
            {
                var line = File.ReadLines(memInfo).FirstOrDefault(value => value.StartsWith("MemTotal:", StringComparison.Ordinal));
                if (line is not null)
                {
                    var digits = new string(line.Where(char.IsDigit).ToArray());
                    if (long.TryParse(digits, out var kib))
                        return $"{kib / 1024d / 1024d:0.0} GiB RAM";
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InstallerLog.Warn($"Could not read memory information: {ex.Message}");
        }

        return "unknown";
    }

    private static string ReadGpuModel()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "lspci",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process is not null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(1500);

                var gpuLines = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(line => line.Contains("VGA compatible controller", StringComparison.OrdinalIgnoreCase)
                                   || line.Contains("3D controller", StringComparison.OrdinalIgnoreCase)
                                   || line.Contains("Display controller", StringComparison.OrdinalIgnoreCase))
                    .Select(line =>
                    {
                        var separator = line.IndexOf(": ", StringComparison.Ordinal);
                        return separator >= 0 ? line[(separator + 2)..].Trim() : line.Trim();
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (gpuLines.Length > 0)
                    return string.Join("; ", gpuLines);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            InstallerLog.Warn($"Could not query GPU information with lspci: {ex.Message}");
        }

        try
        {
            var drmRoot = "/sys/class/drm";
            if (Directory.Exists(drmRoot))
            {
                var drivers = Directory.EnumerateDirectories(drmRoot, "card*")
                    .Where(path => !Path.GetFileName(path).Contains('-', StringComparison.Ordinal))
                    .Select(path => Path.Combine(path, "device", "driver", "module"))
                    .Where(Directory.Exists)
                    .Select(path => new DirectoryInfo(path).ResolveLinkTarget(true)?.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (drivers.Length > 0)
                    return $"Detected graphics driver: {string.Join(", ", drivers!)}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            InstallerLog.Warn($"Could not read DRM GPU information: {ex.Message}");
        }

        return "not detected";
    }

    private static string ReadDesktop()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        if (!string.IsNullOrWhiteSpace(desktop))
            return desktop;

        desktop = Environment.GetEnvironmentVariable("DESKTOP_SESSION");
        return string.IsNullOrWhiteSpace(desktop) ? "unknown" : desktop;
    }

    private void RefreshInstallUi()
    {
        var pathError = RefreshInstallPathState();
        var installed = false;

        try
        {
            _installationInfo = _installService.GetInstallationInfo(InstallRoot);
            installed = _installationInfo.Condition == InstallationCondition.Installed;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            _installationInfo = new(InstallationCondition.NotInstalled, null, false);
            pathError = "The installation path is not valid.";
            InstallPathStatus.Text = $"✗ {pathError}";
            InstallPathStatus.Foreground = Bad;
        }

        _maintenanceMode = _installationInfo.Condition != InstallationCondition.NotInstalled;
        if (_maintenanceMode)
        {
            InstalledVersionText.Text = _installationInfo.Version ?? "Unknown / damaged metadata";
            MaintenanceInstallPath.Text = InstallRoot;
            MaintenanceDescription.Text = _installationInfo.Condition == InstallationCondition.Installed
                ? "An existing Sanctuary installation was detected. Use the maintenance actions below instead of continuing through setup."
                : "This Sanctuary installation is incomplete or damaged. Repair is available, but uninstall remains locked until ownership can be verified.";
            _updatingMaintenanceShortcut = true;
            MaintenanceDesktopShortcutCheck.IsChecked = _installationInfo.HasDesktopShortcut;
            _updatingMaintenanceShortcut = false;
            var desktop = DesktopResolution;
            var display = DisplayModeConfig.Read(InstallRoot, desktop.Width, desktop.Height);
            _updatingMaintenanceDisplayMode = true;
            MaintenanceFullscreenCheck.IsChecked = display.Mode == DisplayModeConfig.Fullscreen;
            _updatingMaintenanceDisplayMode = false;
            LaunchInstalledButton.IsEnabled = !_busy && installed;
            UninstallButton.IsEnabled = !_busy && installed;
            RepairButton.IsEnabled = !_busy && _state.Ready;
        }

        ActionButton.Content = installed ? "UNINSTALL" : "INSTALL";
        ActionButton.IsEnabled = !_busy && (installed || (_state.Ready && pathError is null));

        HeroStatus.Text = installed
            ? "SANCTUARY IS INSTALLED"
            : !_state.Ready
                ? "SYSTEM REQUIREMENTS NOT MET"
                : pathError is not null
                    ? "INSTALL LOCATION REQUIRES ATTENTION"
                    : "SYSTEM READY FOR INSTALLATION";
        HeroStatus.Foreground = installed || (_state.Ready && pathError is null) ? Good : Bad;

        if (!_busy)
        {
            StatusText.Text = installed
                ? "Sanctuary is installed. Uninstall is available."
                : pathError is not null
                    ? "Select a valid installation location to continue."
                    : "Ready to install Sanctuary.";
            ProgressText.Text = "Ready";
        }

        RefreshSummary();
        UpdateStepUi();
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
            ? "✓ Existing Sanctuary installation detected."
            : "✓ Installation location is available.";
        InstallPathStatus.Foreground = Good;
        return null;
    }

    private void RefreshSummary()
    {
        SummaryInstallPath.Text = InstallPathBox.Text ?? InstallService.DefaultInstallRoot;
        SummarySteamPath.Text = _state.SteamRoot ?? "Not detected";
        SummaryProtonPath.Text = _state.ProtonPath ?? "Not detected";
        SummaryGraphicsBackend.Text = GraphicsBackendConfig.DisplayName(SelectedGraphicsBackend);
        SummaryDisplayMode.Text = DisplayModeConfig.DisplayName(SelectedDisplayMode);
    }

    private static void SetCheck(TextBlock control, bool ok, string text)
    {
        control.Text = ok ? $"✓ {text}" : $"✗ {text}";
        control.Foreground = ok ? Good : Bad;
    }

    private void SetStepStyle(Border circle, TextBlock label, bool active)
    {
        circle.Background = active ? Active : Inactive;
        circle.BorderBrush = active ? Active : InactiveBorder;
        circle.BorderThickness = active ? new Avalonia.Thickness(0) : new Avalonia.Thickness(1);
        if (circle.Child is TextBlock number)
            number.Foreground = active ? Brushes.White : InactiveText;
        label.Foreground = active ? ActiveText : InactiveText;
        label.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
    }

    private void UpdateStepUi()
    {
        if (_maintenanceMode)
        {
            StepHeader.IsVisible = false;
            WelcomePanel.IsVisible = false;
            MaintenancePanel.IsVisible = true;
            LocationPanel.IsVisible = false;
            ProtonPanel.IsVisible = false;
            SummaryPanel.IsVisible = false;
            InstallPanel.IsVisible = false;
            BackButton.IsVisible = false;
            NextButton.IsVisible = false;
            ActionButton.IsVisible = false;
            StepHint.Text = "Existing installation maintenance";
            return;
        }

        StepHeader.IsVisible = true;
        MaintenancePanel.IsVisible = false;
        BackButton.IsVisible = true;
        WelcomePanel.IsVisible = _step == 1;
        LocationPanel.IsVisible = _step == 2;
        ProtonPanel.IsVisible = _step == 3;
        SummaryPanel.IsVisible = _step == 4;
        InstallPanel.IsVisible = _step == 5;

        SetStepStyle(Step1Circle, Step1Label, _step == 1);
        SetStepStyle(Step2Circle, Step2Label, _step == 2);
        SetStepStyle(Step3Circle, Step3Label, _step == 3);
        SetStepStyle(Step4Circle, Step4Label, _step == 4);
        SetStepStyle(Step5Circle, Step5Label, _step == 5);

        BackButton.IsEnabled = !_busy && _step > 1;
        NextButton.IsVisible = _step < 5;
        ActionButton.IsVisible = _step == 5;
        StepHint.Text = $"Step {_step} of 5";

        var pathError = InstallService.GetInstallDestinationError(InstallPathBox.Text ?? string.Empty);
        NextButton.IsEnabled = !_busy && _step switch
        {
            1 => _state.Ready,
            2 => pathError is null,
            3 => _state.ProtonPath is not null && _state.ProtonCompatible,
            4 => SummaryAcceptCheck.IsChecked == true,
            _ => false
        };

        RefreshSummary();
    }

    private void NextClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy || _step >= 5)
            return;

        if (_step == 1 && !_state.Ready)
            return;
        if (_step == 2 && InstallService.GetInstallDestinationError(InstallPathBox.Text ?? string.Empty) is not null)
            return;
        if (_step == 3 && (_state.ProtonPath is null || !_state.ProtonCompatible))
            return;
        if (_step == 4 && SummaryAcceptCheck.IsChecked != true)
            return;

        _step++;
        UpdateStepUi();
    }

    private void BackClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy || _step <= 1)
            return;

        _step--;
        UpdateStepUi();
    }

    private void SummaryAcceptanceChanged(object? sender, RoutedEventArgs e) => UpdateStepUi();

    private void ProtonSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingProtonSelection || ProtonComboBox.SelectedItem is not ProtonCandidate selected)
            return;

        _state = _state.WithProton(selected);
        DetailText.Text = BuildHostDetails();
        ProtonStatus.Text = $"✓ {selected.Name}";
        ProtonStatus.Foreground = Good;
        InstallerLog.Info($"User selected Proton: {selected.Path} ({selected.RuntimeArchitecture}, compatible={selected.Compatible})");
        RefreshInstallUi();
    }

    private void GraphicsBackendSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        InstallerLog.Info($"User selected graphics backend: {GraphicsBackendConfig.DisplayName(SelectedGraphicsBackend)}");
        SummaryAcceptCheck.IsChecked = false;
        RefreshSummary();
        UpdateStepUi();
    }

    private void DisplayModeSelectionChanged(object? sender, RoutedEventArgs e)
    {
        InstallerLog.Info($"User selected game display mode: {DisplayModeConfig.DisplayName(SelectedDisplayMode)}");
        SummaryAcceptCheck.IsChecked = false;
        RefreshSummary();
        UpdateStepUi();
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
            Title = "Choose Sanctuary installation location",
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

    private async void LaunchInstalledClicked(object? sender, RoutedEventArgs e)
    {
        if (_busy || _installationInfo.Condition != InstallationCondition.Installed)
            return;
        try { _installService.Launch(InstallRoot); }
        catch (Exception ex) { await ShowMessageAsync("Launch failed", ex.Message); }
    }

    private async void RepairClicked(object? sender, RoutedEventArgs e)
    {
        if (!_busy)
            await InstallAsync(repairExisting: true);
    }

    private async void UninstallClicked(object? sender, RoutedEventArgs e)
    {
        if (!_busy && _installationInfo.Condition == InstallationCondition.Installed)
            await UninstallAsync();
    }

    private async void MaintenanceShortcutChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingMaintenanceShortcut || _busy || _installationInfo.Condition != InstallationCondition.Installed)
            return;
        try
        {
            _installService.SetDesktopShortcut(InstallRoot, MaintenanceDesktopShortcutCheck.IsChecked == true);
            RefreshInstallUi();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Shortcut update failed", ex.Message);
            RefreshInstallUi();
        }
    }

    private async void MaintenanceDisplayModeChanged(object? sender, RoutedEventArgs e)
    {
        if (_updatingMaintenanceDisplayMode || _busy || _installationInfo.Condition != InstallationCondition.Installed)
            return;

        try
        {
            var mode = MaintenanceFullscreenCheck.IsChecked == true
                ? DisplayModeConfig.Fullscreen
                : DisplayModeConfig.Windowed;
            var desktop = DesktopResolution;
            var resolution = mode == DisplayModeConfig.Windowed ? (1280, 720) : desktop;
            DisplayModeConfig.Write(InstallRoot, mode, resolution.Item1, resolution.Item2);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Display mode update failed", ex.Message);
            RefreshInstallUi();
        }
    }

    private async void OpenInstallFolderClicked(object? sender, RoutedEventArgs e) => await OpenFolderAsync(InstallRoot);

    private async void OpenLogsClicked(object? sender, RoutedEventArgs e) =>
        await OpenFolderAsync(Path.GetDirectoryName(InstallerLog.LogPath)!);

    private async Task OpenFolderAsync(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var startInfo = new ProcessStartInfo("xdg-open") { UseShellExecute = false };
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not open folder", ex.Message);
        }
    }

    private async void ExportDiagnosticsClicked(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Sanctuary diagnostics",
            SuggestedFileName = $"sanctuary-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip"
        });
        if (file?.TryGetLocalPath() is not { } path)
            return;

        try
        {
            await DiagnosticBundleService.CreateAsync(path, BuildHostDetails());
            await ShowMessageAsync("Diagnostics exported", $"Redacted diagnostics were saved to:\n{path}");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Diagnostics export failed", ex.Message);
        }
    }

    private async Task InstallAsync(bool repairExisting = false)
    {
        var selectedProton = (ProtonComboBox.SelectedItem as ProtonCandidate)?.Path;
        var selectedGraphicsBackend = SelectedGraphicsBackend;
        var selectedDisplayMode = repairExisting
            ? (MaintenanceFullscreenCheck.IsChecked == true ? DisplayModeConfig.Fullscreen : DisplayModeConfig.Windowed)
            : SelectedDisplayMode;
        RefreshState();
        ReapplyWindowIcon();
        if (selectedProton is not null && _state.ProtonCandidates?.FirstOrDefault(candidate => candidate.Path == selectedProton && candidate.Compatible) is { } selected)
            _state = _state.WithProton(selected);

        if (!_state.Ready)
        {
            await ShowMessageAsync("Requirements not met", "A supported x86_64 Linux environment, Steam, and a compatible Proton build must be available before Sanctuary can be installed.");
            return;
        }

        var pathError = InstallService.GetInstallDestinationError(InstallPathBox.Text ?? string.Empty);
        if (pathError is not null && !(repairExisting && InstallationOwnership.HasRecognizableMetadata(InstallRoot)))
        {
            await ShowMessageAsync("Invalid installation location", pathError);
            return;
        }

        SetBusy(true);
        ReapplyWindowIcon();
        var progress = new Progress<InstallProgress>(UpdateProgress);
        var shouldClose = false;

        try
        {
            await _installService.InstallAsync(
                InstallRoot,
                _state,
                progress,
                new InstallOptions(
                    repairExisting ? MaintenanceDesktopShortcutCheck.IsChecked == true : CreateDesktopShortcutCheck.IsChecked == true,
                    repairExisting));

            try
            {
                GraphicsBackendConfig.Write(InstallRoot, selectedGraphicsBackend);
                var desktop = DesktopResolution;
                var resolution = selectedDisplayMode == DisplayModeConfig.Windowed ? (1280, 720) : desktop;
                DisplayModeConfig.Write(InstallRoot, selectedDisplayMode, resolution.Item1, resolution.Item2);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                InstallerLog.Error("Installation completed but graphics backend configuration failed", ex);
                await ShowMessageAsync(
                    "Graphics configuration failed",
                    $"Sanctuary was installed, but the selected graphics backend could not be saved. The launcher was not started.\n\n{ex.Message}\n\nDiagnostics: {InstallerLog.LogPath}");
                return;
            }

            if (LaunchAfterInstallCheck.IsChecked == true)
                _installService.Launch(InstallRoot);
            ReapplyWindowIcon();
            shouldClose = CloseAfterInstallCheck.IsChecked == true;
            if (!shouldClose)
                await ShowMessageAsync(
                    "Installation complete",
                    LaunchAfterInstallCheck.IsChecked == true
                        ? "Sanctuary has been installed successfully and the Open Source Free Realms launcher has started."
                        : "Sanctuary has been installed successfully.");
        }
        catch (Exception ex)
        {
            InstallerLog.Error("Installation UI flow failed", ex);
            await ShowMessageAsync("Installation failed", $"{ex.Message}\n\nDiagnostics: {InstallerLog.LogPath}");
        }
        finally
        {
            SetBusy(false);
            ReapplyWindowIcon();
            if (!shouldClose)
                RefreshState();
        }

        if (shouldClose)
            Close();
    }

    private async Task UninstallAsync()
    {
        var confirmed = await ConfirmAsync(
            "Uninstall Sanctuary",
            (RemoveUserDataCheck.IsChecked == true
                ? "Remove Sanctuary, its shortcuts, downloaded game files, launcher settings, logs, and saved user data? This cannot be undone. The installer executable itself will be preserved."
                : "Remove Sanctuary and its shortcuts while preserving downloaded game files, launcher settings, logs, and saved user data?")
            );
        if (!confirmed)
            return;

        SetBusy(true);
        var progress = new Progress<InstallProgress>(UpdateProgress);

        try
        {
            await _installService.UninstallAsync(
                InstallRoot,
                progress,
                new UninstallOptions(RemoveUserDataCheck.IsChecked == true));
            InstallPathBox.Text = InstallerState.GetInitialInstallRoot();
            await ShowMessageAsync(
                "Uninstall complete",
                RemoveUserDataCheck.IsChecked == true
                    ? "Sanctuary and its user data have been removed."
                    : "Sanctuary was removed. Downloaded game files and launcher data were preserved.");
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
        LaunchAfterInstallCheck.IsEnabled = !busy;
        CreateDesktopShortcutCheck.IsEnabled = !busy;
        MaintenanceDesktopShortcutCheck.IsEnabled = !busy && _installationInfo.Condition == InstallationCondition.Installed;
        MaintenanceFullscreenCheck.IsEnabled = !busy && _installationInfo.Condition == InstallationCondition.Installed;
        RemoveUserDataCheck.IsEnabled = !busy;
        LaunchInstalledButton.IsEnabled = !busy && _installationInfo.Condition == InstallationCondition.Installed;
        RepairButton.IsEnabled = !busy && _state.Ready;
        UninstallButton.IsEnabled = !busy && _installationInfo.Condition == InstallationCondition.Installed;
        SummaryAcceptCheck.IsEnabled = !busy;
        ProtonComboBox.IsEnabled = !busy && (_state.ProtonCandidates?.Any(candidate => candidate.Compatible) ?? false);
        GraphicsBackendComboBox.IsEnabled = !busy;
        FullscreenCheck.IsEnabled = !busy;
        UpdateStepUi();
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
